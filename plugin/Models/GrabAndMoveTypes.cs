using System.ComponentModel;

namespace GrabAndMove.Models;

/// <summary>
/// Types and state for Grab And Move.
/// Logic adapted from Microsoft PowerToys GrabAndMove (MIT).
/// Copyright (c) Microsoft Corporation.
/// </summary>
internal enum GrabAndMoveModifier
{
    [Description("Alt")]
    Alt = 0,

    [Description("Windows")]
    Win = 1
}

internal enum ResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

internal enum InteractionPhase
{
    Idle,
    Pending,
    Active
}

internal enum InteractionAction
{
    None,
    Move,
    Resize
}

internal enum MouseButtonKind
{
    None,
    Left,
    Right
}

internal enum ModifierHoldDisposition
{
    Undecided,
    Passthrough,
    Activated
}

internal enum TargetEligibility
{
    Invalid,
    Excluded,
    NotResizable,
    Eligible
}

internal enum HookDisposition
{
    Chain,
    Swallow,
    PassWithoutChaining
}

internal enum ModifierReplay
{
    DownOnly,
    UpOnly,
    DownAndUp
}

internal readonly record struct ScreenPoint(int X, int Y);

internal readonly record struct WindowRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

internal sealed class GrabAndMoveSettings
{
    public GrabAndMoveModifier ModifierKey { get; set; } = GrabAndMoveModifier.Alt;
    public bool ShouldAbsorbAlt { get; set; } = true;
    public bool UseRightClickResize { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public IReadOnlyList<string> ExcludedApps { get; set; } = [];
    public IReadOnlyList<string> ModifierExcludedApps { get; set; } = [];
}

internal struct CapturedKey
{
    public uint Vk;
    public uint ScanCode;
    public bool Extended;
}

internal sealed class ModifierSession
{
    public GrabAndMoveModifier Modifier { get; set; } = GrabAndMoveModifier.Alt;
    public ModifierHoldDisposition Disposition { get; set; } = ModifierHoldDisposition.Undecided;
    public CapturedKey Key { get; set; }
    public bool Pressed { get; set; }
    public bool Absorbed { get; set; }
    public bool ReplayedDown { get; set; }
    public bool Consumed { get; set; }

    public void Reset()
    {
        Modifier = GrabAndMoveModifier.Alt;
        Disposition = ModifierHoldDisposition.Undecided;
        Key = default;
        Pressed = false;
        Absorbed = false;
        ReplayedDown = false;
        Consumed = false;
    }
}

internal sealed class InteractionState
{
    public InteractionPhase Phase { get; set; } = InteractionPhase.Idle;
    public InteractionAction Action { get; set; } = InteractionAction.None;
    public MouseButtonKind Button { get; set; } = MouseButtonKind.None;
    public nint Target { get; set; }
    public ScreenPoint StartPoint { get; set; }
    public ScreenPoint LastPoint { get; set; }
    public WindowRect WindowRect { get; set; }
    public ResizeHandle ResizeHandle { get; set; } = ResizeHandle.None;
    public bool FirstUpdate { get; set; }

    public void Reset()
    {
        Phase = InteractionPhase.Idle;
        Action = InteractionAction.None;
        Button = MouseButtonKind.None;
        Target = 0;
        StartPoint = default;
        LastPoint = default;
        WindowRect = default;
        ResizeHandle = ResizeHandle.None;
        FirstUpdate = false;
    }
}

internal readonly record struct ActionTarget(nint Window, TargetEligibility Eligibility);
