using System.Diagnostics;
using GrabAndMove.Models;

namespace GrabAndMove.Services;

/// <summary>
/// Grab And Move interaction state machine.
/// Ported from Microsoft PowerToys <c>src/modules/GrabAndMove/GrabAndMove/main.cpp</c> (MIT).
/// Copyright (c) Microsoft Corporation. Overlay and Game Mode gates are intentionally omitted in v1.
/// </summary>
internal sealed class GrabAndMoveCore
{
    private const long ThrottleIntervalMs = 16;

    private readonly ModifierKeyInjector _injector;
    private readonly object _sync = new();
    private readonly bool[] _keyHeld = new bool[256];

    private GrabAndMoveSettings _settings = new();
    private readonly ModifierSession _modifierSession = new();
    private readonly InteractionState _interaction = new();
    private int _heldNonModifierCount;
    private uint _swallowButtonUpMask;
    private uint _swallowNextModifierUpVk;
    private long _lastMoveTick;

    public GrabAndMoveCore(ModifierKeyInjector injector)
    {
        ArgumentNullException.ThrowIfNull(injector);
        _injector = injector;
    }

    public void UpdateSettings(GrabAndMoveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_sync)
            _settings = CloneSettings(settings);
    }

    public void Reset()
    {
        lock (_sync)
        {
            StopInteractionUnlocked();
            _modifierSession.Reset();
            _swallowButtonUpMask = 0;
            _swallowNextModifierUpVk = 0;
            Array.Clear(_keyHeld);
            _heldNonModifierCount = 0;
        }
    }

    public HookDisposition HandleKeyDown(uint vk, uint scanCode, bool extended, bool isSimulator)
    {
        if (isSimulator)
            return HookDisposition.Chain;

        lock (_sync)
        {
            if (!_settings.Enabled)
                return HookDisposition.Chain;

            if (vk == _swallowNextModifierUpVk)
                _swallowNextModifierUpVk = 0;

            if (HasModifierSession() && IsModifierKey(vk, _modifierSession.Modifier))
            {
                return _modifierSession.Absorbed ? HookDisposition.Swallow : HookDisposition.Chain;
            }

            if (HasModifierSession() &&
                !IsModifierKey(vk, _modifierSession.Modifier) &&
                _modifierSession.Absorbed &&
                !_modifierSession.Consumed &&
                !_modifierSession.ReplayedDown)
            {
                _injector.TryReplayModifier(_modifierSession.Key, ModifierReplay.DownOnly);
                _modifierSession.Absorbed = false;
                _modifierSession.Pressed = false;
                _modifierSession.ReplayedDown = true;
            }

            TrackHeldNonModifierKey(vk, isDown: true);

            if (!IsModifierKey(vk, _settings.ModifierKey) || HasModifierSession())
                return HookDisposition.Chain;

            var foreground = GrabWindowNative.GetForegroundRoot();
            if (foreground != 0 &&
                GrabWindowNative.MatchesExcludedApps(foreground, _settings.ModifierExcludedApps))
            {
                return HookDisposition.Chain;
            }

            if (HasConflictingInput(_settings.ModifierKey))
                return HookDisposition.Chain;

            // Game Mode suppression is documented for a later release.
            _modifierSession.Reset();
            _modifierSession.Modifier = _settings.ModifierKey;
            _modifierSession.Pressed = true;
            _modifierSession.Absorbed =
                _settings.ModifierKey == GrabAndMoveModifier.Win || _settings.ShouldAbsorbAlt;
            _modifierSession.Key = new CapturedKey
            {
                Vk = vk,
                ScanCode = scanCode,
                Extended = extended
            };

            return _modifierSession.Absorbed ? HookDisposition.Swallow : HookDisposition.Chain;
        }
    }

    public HookDisposition HandleKeyUp(uint vk, bool isSimulator)
    {
        if (isSimulator)
            return HookDisposition.Chain;

        lock (_sync)
        {
            if (vk == _swallowNextModifierUpVk)
            {
                _swallowNextModifierUpVk = 0;
                return HookDisposition.Swallow;
            }

            if (HasModifierSession() && IsModifierKey(vk, _modifierSession.Modifier))
                return ReleaseModifierSessionUnlocked();

            TrackHeldNonModifierKey(vk, isDown: false);
            return HookDisposition.Chain;
        }
    }

    public HookDisposition HandleMouseDown(MouseButtonKind button, ScreenPoint point, nuint extraInfo)
    {
        if (extraInfo == ModifierKeyInjector.ReplayTag)
            return HookDisposition.Chain;

        lock (_sync)
        {
            if (!_settings.Enabled)
                return HookDisposition.Chain;

            ClearStaleSwallowedButtonUp(button);

            if (!IsActivationModifierPressed())
            {
                RecoverStaleInteractionUnlocked(button);
                return HookDisposition.Chain;
            }

            if (button == MouseButtonKind.Right &&
                _interaction.Phase != InteractionPhase.Active &&
                !_settings.UseRightClickResize)
            {
                return EnterModifierPassthroughUnlocked(button, latchHold: false);
            }

            return HandleActionButtonDownUnlocked(button, point);
        }
    }

    public HookDisposition HandleMouseUp(MouseButtonKind button, ScreenPoint point, nuint extraInfo)
    {
        if (extraInfo == ModifierKeyInjector.ReplayTag)
            return HookDisposition.Chain;

        lock (_sync)
        {
            if (ConsumeSwallowedButtonUp(button))
                return HookDisposition.Swallow;

            if (button == _interaction.Button && _interaction.Phase == InteractionPhase.Pending)
            {
                _modifierSession.Disposition = ModifierHoldDisposition.Passthrough;
                var pendingButton = _interaction.Button;
                StopInteractionUnlocked();
                ReplayPendingClickUnlocked(pendingButton);
                return HookDisposition.Swallow;
            }

            if (button == _interaction.Button && _interaction.Phase == InteractionPhase.Active)
            {
                if (_interaction.Action == InteractionAction.Move)
                {
                    var dx = point.X - _interaction.StartPoint.X;
                    var dy = point.Y - _interaction.StartPoint.Y;
                    var finalRect = new WindowRect(
                        _interaction.WindowRect.Left + dx,
                        _interaction.WindowRect.Top + dy,
                        _interaction.WindowRect.Right + dx,
                        _interaction.WindowRect.Bottom + dy);
                    GrabWindowNative.MoveWindow(_interaction.Target, finalRect);
                }
                else
                {
                    GrabWindowNative.MoveResizeWindow(_interaction.Target, _interaction.WindowRect);
                }

                StopInteractionUnlocked();
                return HookDisposition.Swallow;
            }

            return HookDisposition.Chain;
        }
    }

    public HookDisposition HandleMouseMove(ScreenPoint point, nuint extraInfo)
    {
        if (extraInfo == ModifierKeyInjector.ReplayTag)
            return HookDisposition.Chain;

        lock (_sync)
        {
            if (_interaction.Phase == InteractionPhase.Pending)
            {
                var dx = Math.Abs(point.X - _interaction.StartPoint.X);
                var dy = Math.Abs(point.Y - _interaction.StartPoint.Y);
                if (dx >= GrabWindowNative.DragThresholdX || dy >= GrabWindowNative.DragThresholdY)
                {
                    var pending = SnapshotInteraction();
                    StopInteractionUnlocked();
                    if (BeginInteractionUnlocked(
                            pending.Action,
                            pending.Button,
                            pending.Target,
                            pending.StartPoint))
                    {
                        if (pending.Action == InteractionAction.Resize)
                            HandleDragResizeUnlocked(point);
                        else
                            HandleDragMoveUnlocked(point);
                    }
                    else
                    {
                        _modifierSession.Disposition = ModifierHoldDisposition.Passthrough;
                        ReplayPendingClickUnlocked(pending.Button);
                        MarkButtonUpForSwallow(pending.Button);
                    }
                }

                return HookDisposition.PassWithoutChaining;
            }

            if (_interaction.Phase == InteractionPhase.Active)
            {
                var now = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
                if (now - _lastMoveTick >= ThrottleIntervalMs)
                {
                    _lastMoveTick = now;
                    if (_interaction.Action == InteractionAction.Resize)
                        HandleDragResizeUnlocked(point);
                    else
                        HandleDragMoveUnlocked(point);
                }

                return HookDisposition.PassWithoutChaining;
            }

            return HookDisposition.Chain;
        }
    }

    private HookDisposition HandleActionButtonDownUnlocked(MouseButtonKind button, ScreenPoint point)
    {
        var action = button == MouseButtonKind.Right
            ? InteractionAction.Resize
            : InteractionAction.Move;

        var target = ResolveActionTarget(point, action);
        if (target.Eligibility != TargetEligibility.Eligible)
            return EnterModifierPassthroughUnlocked(button, latchHold: false);

        if (_modifierSession.Disposition == ModifierHoldDisposition.Activated)
        {
            return BeginInteractionUnlocked(action, button, target.Window, point)
                ? HookDisposition.Swallow
                : EnterModifierPassthroughUnlocked(button, latchHold: false);
        }

        if (_interaction.Phase == InteractionPhase.Pending)
            return HookDisposition.Chain;

        ArmPendingInteractionUnlocked(action, button, target.Window, point);
        return HookDisposition.Swallow;
    }

    private ActionTarget ResolveActionTarget(ScreenPoint point, InteractionAction action)
    {
        var window = GrabWindowNative.ResolveTargetWindow(point);
        if (window == 0)
            return default;

        if (GrabWindowNative.IsSystemClass(window) ||
            GrabWindowNative.IsShellExperienceExcluded(window) ||
            GrabWindowNative.MatchesExcludedApps(window, _settings.ExcludedApps))
        {
            return new ActionTarget(window, TargetEligibility.Excluded);
        }

        if (action == InteractionAction.Resize && !GrabWindowNative.HasThickFrame(window))
            return new ActionTarget(window, TargetEligibility.NotResizable);

        return new ActionTarget(window, TargetEligibility.Eligible);
    }

    private bool BeginInteractionUnlocked(
        InteractionAction action,
        MouseButtonKind button,
        nint hwnd,
        ScreenPoint pt)
    {
        if (!GrabWindowNative.TryGetWindowRect(hwnd, out var windowRect))
            return false;

        _interaction.Phase = InteractionPhase.Active;
        _interaction.Action = action;
        _interaction.Button = button;
        _interaction.Target = hwnd;
        _interaction.StartPoint = pt;
        _interaction.LastPoint = pt;
        _interaction.WindowRect = windowRect;
        _interaction.FirstUpdate = true;
        _interaction.ResizeHandle = action == InteractionAction.Resize
            ? GetClosestHandle(pt, windowRect)
            : ResizeHandle.None;

        _modifierSession.Consumed = true;
        _modifierSession.Disposition = ModifierHoldDisposition.Activated;
        return true;
    }

    private void ArmPendingInteractionUnlocked(
        InteractionAction action,
        MouseButtonKind button,
        nint hwnd,
        ScreenPoint pt)
    {
        _interaction.Reset();
        _interaction.Phase = InteractionPhase.Pending;
        _interaction.Action = action;
        _interaction.Button = button;
        _interaction.Target = hwnd;
        _interaction.StartPoint = pt;
    }

    private void HandleDragMoveUnlocked(ScreenPoint pt)
    {
        if (_interaction.Phase != InteractionPhase.Active ||
            _interaction.Action != InteractionAction.Move ||
            _interaction.Target == 0)
        {
            return;
        }

        if (_interaction.FirstUpdate)
        {
            _interaction.FirstUpdate = false;
            if (GrabWindowNative.IsZoomed(_interaction.Target))
            {
                GrabWindowNative.TryGetWindowRect(_interaction.Target, out var maxRect);
                var maxW = maxRect.Width;
                var maxH = maxRect.Height;

                GrabWindowNative.Restore(_interaction.Target);
                GrabWindowNative.TryGetWindowRect(_interaction.Target, out var restored);
                var restoredW = restored.Width;
                var restoredH = restored.Height;

                var ratioL = maxW > 0
                    ? (_interaction.StartPoint.X - maxRect.Left) / (float)maxW
                    : 0.5f;
                var ratioT = maxH > 0
                    ? (_interaction.StartPoint.Y - maxRect.Top) / (float)maxH
                    : 0.5f;
                var newX = _interaction.StartPoint.X - (int)(restoredW * ratioL);
                var newY = _interaction.StartPoint.Y - (int)(restoredH * ratioT);
                _interaction.StartPoint = pt;
                _interaction.WindowRect = new WindowRect(newX, newY, newX + restoredW, newY + restoredH);
                GrabWindowNative.MoveWindow(_interaction.Target, _interaction.WindowRect);
            }
        }

        var dx = pt.X - _interaction.StartPoint.X;
        var dy = pt.Y - _interaction.StartPoint.Y;
        var desired = new WindowRect(
            _interaction.WindowRect.Left + dx,
            _interaction.WindowRect.Top + dy,
            _interaction.WindowRect.Right + dx,
            _interaction.WindowRect.Bottom + dy);
        GrabWindowNative.MoveWindow(_interaction.Target, desired);
    }

    private void HandleDragResizeUnlocked(ScreenPoint pt)
    {
        if (_interaction.Phase != InteractionPhase.Active ||
            _interaction.Action != InteractionAction.Resize ||
            _interaction.Target == 0)
        {
            return;
        }

        if (_interaction.FirstUpdate)
        {
            _interaction.FirstUpdate = false;
            if (GrabWindowNative.IsZoomed(_interaction.Target))
            {
                var maxRect = _interaction.WindowRect;
                var maxW = maxRect.Width;
                var maxH = maxRect.Height;
                var ratioL = maxW > 0 ? (pt.X - maxRect.Left) / (float)maxW : 0.5f;
                var ratioT = maxH > 0 ? (pt.Y - maxRect.Top) / (float)maxH : 0.5f;

                GrabWindowNative.Restore(_interaction.Target);
                GrabWindowNative.TryGetWindowRect(_interaction.Target, out var restored);
                var newW = restored.Width;
                var newH = restored.Height;
                var newLeft = pt.X - (int)(ratioL * newW);
                var newTop = pt.Y - (int)(ratioT * newH);
                _interaction.WindowRect = new WindowRect(newLeft, newTop, newLeft + newW, newTop + newH);
                GrabWindowNative.MoveResizeWindow(_interaction.Target, _interaction.WindowRect);
                _interaction.LastPoint = pt;
                _interaction.ResizeHandle = GetClosestHandle(pt, _interaction.WindowRect);
            }
        }

        var dx = pt.X - _interaction.LastPoint.X;
        var dy = pt.Y - _interaction.LastPoint.Y;
        _interaction.ResizeHandle = GetClosestHandle(pt, _interaction.WindowRect);

        var nr = _interaction.WindowRect;
        switch (_interaction.ResizeHandle)
        {
            case ResizeHandle.TopLeft:
                nr = nr with { Left = nr.Left + dx, Top = nr.Top + dy };
                break;
            case ResizeHandle.Top:
                nr = nr with { Top = nr.Top + dy };
                break;
            case ResizeHandle.TopRight:
                nr = nr with { Right = nr.Right + dx, Top = nr.Top + dy };
                break;
            case ResizeHandle.Right:
                nr = nr with { Right = nr.Right + dx };
                break;
            case ResizeHandle.BottomRight:
                nr = nr with { Right = nr.Right + dx, Bottom = nr.Bottom + dy };
                break;
            case ResizeHandle.Bottom:
                nr = nr with { Bottom = nr.Bottom + dy };
                break;
            case ResizeHandle.BottomLeft:
                nr = nr with { Left = nr.Left + dx, Bottom = nr.Bottom + dy };
                break;
            case ResizeHandle.Left:
                nr = nr with { Left = nr.Left + dx };
                break;
        }

        var leftMoving = _interaction.ResizeHandle is
            ResizeHandle.TopLeft or ResizeHandle.BottomLeft or ResizeHandle.Left;
        var topMoving = _interaction.ResizeHandle is
            ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight;

        if (nr.Width < GrabWindowNative.MinWindowWidth)
        {
            nr = leftMoving
                ? nr with { Left = nr.Right - GrabWindowNative.MinWindowWidth }
                : nr with { Right = nr.Left + GrabWindowNative.MinWindowWidth };
        }

        if (nr.Height < GrabWindowNative.MinWindowHeight)
        {
            nr = topMoving
                ? nr with { Top = nr.Bottom - GrabWindowNative.MinWindowHeight }
                : nr with { Bottom = nr.Top + GrabWindowNative.MinWindowHeight };
        }

        _interaction.WindowRect = nr;
        _interaction.LastPoint = pt;
        GrabWindowNative.MoveResizeWindow(_interaction.Target, nr);
    }

    internal static ResizeHandle GetClosestHandle(ScreenPoint pt, WindowRect rc)
    {
        var cx = (rc.Left + rc.Right) / 2;
        var cy = (rc.Top + rc.Bottom) / 2;
        (int X, int Y, ResizeHandle H)[] handles =
        [
            (rc.Left, rc.Top, ResizeHandle.TopLeft),
            (cx, rc.Top, ResizeHandle.Top),
            (rc.Right, rc.Top, ResizeHandle.TopRight),
            (rc.Right, cy, ResizeHandle.Right),
            (rc.Right, rc.Bottom, ResizeHandle.BottomRight),
            (cx, rc.Bottom, ResizeHandle.Bottom),
            (rc.Left, rc.Bottom, ResizeHandle.BottomLeft),
            (rc.Left, cy, ResizeHandle.Left)
        ];

        var closest = ResizeHandle.BottomRight;
        var minDist = long.MaxValue;
        foreach (var e in handles)
        {
            long ddx = pt.X - e.X;
            long ddy = pt.Y - e.Y;
            var dist = ddx * ddx + ddy * ddy;
            if (dist < minDist)
            {
                minDist = dist;
                closest = e.H;
            }
        }

        return closest;
    }

    private HookDisposition EnterModifierPassthroughUnlocked(MouseButtonKind button, bool latchHold)
    {
        var disposition = HookDisposition.Chain;
        if (_modifierSession.Absorbed && !_modifierSession.ReplayedDown)
        {
            if (_injector.TryReplayModifierThenClick(_modifierSession.Key, button))
            {
                _modifierSession.ReplayedDown = true;
                _modifierSession.Absorbed = false;
                disposition = HookDisposition.Swallow;
            }
            else
            {
                MarkButtonUpForSwallow(button);
                disposition = HookDisposition.Swallow;
            }
        }

        if (latchHold)
            _modifierSession.Disposition = ModifierHoldDisposition.Passthrough;

        return disposition;
    }

    private void ReplayPendingClickUnlocked(MouseButtonKind button)
    {
        if (_modifierSession.Absorbed && !_modifierSession.ReplayedDown)
        {
            _modifierSession.ReplayedDown = true;
            _injector.TryReplayModifier(_modifierSession.Key, ModifierReplay.DownOnly);
        }

        _injector.TryReplayClick(button, down: true);
        _injector.TryReplayClick(button, down: false);
    }

    private HookDisposition ReleaseModifierSessionUnlocked()
    {
        var disposition = HookDisposition.Chain;
        var wasDragging = _interaction.Phase == InteractionPhase.Active;
        var wasPending = _interaction.Phase == InteractionPhase.Pending;

        if (wasPending)
        {
            var button = _interaction.Button;
            StopInteractionUnlocked();
            ReplayPendingClickUnlocked(button);
            MarkButtonUpForSwallow(button);
        }
        else if (wasDragging)
        {
            StopInteractionUnlocked();
        }

        if (_modifierSession.Absorbed)
        {
            if (wasDragging || _modifierSession.Consumed)
            {
                disposition = HookDisposition.Swallow;
            }
            else if (!_modifierSession.ReplayedDown)
            {
                _injector.TryReplayModifier(_modifierSession.Key, ModifierReplay.DownAndUp);
                disposition = HookDisposition.Swallow;
            }
        }

        _modifierSession.Reset();
        return disposition;
    }

    private void RecoverStaleInteractionUnlocked(MouseButtonKind incomingButton)
    {
        if (_interaction.Phase == InteractionPhase.Idle)
            return;

        var button = _interaction.Button;
        if (_interaction.Phase == InteractionPhase.Pending)
        {
            StopInteractionUnlocked();
            ReplayPendingClickUnlocked(button);
        }
        else
        {
            StopInteractionUnlocked();
        }

        if (incomingButton != button)
            MarkButtonUpForSwallow(button);
    }

    private void StopInteractionUnlocked() => _interaction.Reset();

    private bool IsActivationModifierPressed() =>
        _modifierSession.Pressed &&
        _modifierSession.Disposition != ModifierHoldDisposition.Passthrough;

    private bool HasModifierSession() => _modifierSession.Pressed || _modifierSession.Absorbed;

    private bool HasConflictingInput(GrabAndMoveModifier modifier)
    {
        if (_heldNonModifierCount > 0)
            return true;

        // Mirror PowerToys: other modifiers already down suppress activation.
        const int vkControl = 0x11;
        const int vkShift = 0x10;
        const int vkMenu = 0x12;
        const int vkLWin = 0x5B;
        const int vkRWin = 0x5C;

        return modifier switch
        {
            GrabAndMoveModifier.Alt =>
                GrabWindowNative.IsKeyDown(vkControl) ||
                GrabWindowNative.IsKeyDown(vkShift) ||
                GrabWindowNative.IsKeyDown(vkLWin) ||
                GrabWindowNative.IsKeyDown(vkRWin),
            GrabAndMoveModifier.Win =>
                GrabWindowNative.IsKeyDown(vkControl) ||
                GrabWindowNative.IsKeyDown(vkShift) ||
                GrabWindowNative.IsKeyDown(vkMenu),
            _ => false
        };
    }

    private void TrackHeldNonModifierKey(uint vk, bool isDown)
    {
        if (vk > 255)
            return;

        if (IsAnyModifierVk(vk))
            return;

        if (isDown)
        {
            if (!_keyHeld[vk])
            {
                _keyHeld[vk] = true;
                _heldNonModifierCount++;
            }
        }
        else if (_keyHeld[vk])
        {
            _keyHeld[vk] = false;
            if (_heldNonModifierCount > 0)
                _heldNonModifierCount--;
        }
    }

    private void MarkButtonUpForSwallow(MouseButtonKind button)
    {
        _swallowButtonUpMask |= ButtonMask(button);
    }

    private bool ConsumeSwallowedButtonUp(MouseButtonKind button)
    {
        var mask = ButtonMask(button);
        if ((_swallowButtonUpMask & mask) == 0)
            return false;

        _swallowButtonUpMask &= ~mask;
        return true;
    }

    private void ClearStaleSwallowedButtonUp(MouseButtonKind button) =>
        _swallowButtonUpMask &= ~ButtonMask(button);

    private InteractionState SnapshotInteraction() => new()
    {
        Phase = _interaction.Phase,
        Action = _interaction.Action,
        Button = _interaction.Button,
        Target = _interaction.Target,
        StartPoint = _interaction.StartPoint,
        LastPoint = _interaction.LastPoint,
        WindowRect = _interaction.WindowRect,
        ResizeHandle = _interaction.ResizeHandle,
        FirstUpdate = _interaction.FirstUpdate
    };

    private static uint ButtonMask(MouseButtonKind button) => button switch
    {
        MouseButtonKind.Left => 1u,
        MouseButtonKind.Right => 2u,
        _ => 0u
    };

    private static bool IsModifierKey(uint vk, GrabAndMoveModifier modifier) => modifier switch
    {
        GrabAndMoveModifier.Alt => vk is 0x12 or 0xA4 or 0xA5, // VK_MENU / LMENU / RMENU
        GrabAndMoveModifier.Win => vk is 0x5B or 0x5C, // LWIN / RWIN
        _ => false
    };

    private static bool IsAnyModifierVk(uint vk) =>
        vk is 0x10 or 0xA0 or 0xA1 or // Shift
            0x11 or 0xA2 or 0xA3 or // Ctrl
            0x12 or 0xA4 or 0xA5 or // Alt
            0x5B or 0x5C; // Win

    private static GrabAndMoveSettings CloneSettings(GrabAndMoveSettings source) => new()
    {
        ModifierKey = source.ModifierKey,
        ShouldAbsorbAlt = source.ShouldAbsorbAlt,
        UseRightClickResize = source.UseRightClickResize,
        Enabled = source.Enabled,
        ExcludedApps = source.ExcludedApps.ToArray(),
        ModifierExcludedApps = source.ModifierExcludedApps.ToArray()
    };
}
