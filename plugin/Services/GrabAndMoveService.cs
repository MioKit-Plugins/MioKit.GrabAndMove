using GrabAndMove.Models;
using Serilog;

namespace GrabAndMove.Services;

/// <summary>
/// Host hook adapter for Grab And Move.
/// Registers on shared <see cref="IMouseHook"/> / <see cref="IKeyboardHook"/> (does not own them).
/// </summary>
internal sealed class GrabAndMoveService :
    IPluginLifecycleComponent,
    IMouseInputHandler,
    IKeyboardInputHandler
{
    private const InputEventHandleResult Consumed =
        InputEventHandleResult.Handled | InputEventHandleResult.SuppressEvent;

    private readonly GrabAndMovePlugin _plugin;
    private readonly GrabAndMoveCore _core;
    private readonly IMouseHook _mouseHook;
    private readonly IKeyboardHook _keyboardHook;
    private readonly ILogger _logger;
    private readonly object _sync = new();

    private bool _featureEnabled;
    private bool _handlersRegistered;

    public GrabAndMoveService(
        GrabAndMovePlugin plugin,
        GrabAndMoveCore core,
        IMouseHook mouseHook,
        IKeyboardHook keyboardHook,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(mouseHook);
        ArgumentNullException.ThrowIfNull(keyboardHook);
        ArgumentNullException.ThrowIfNull(logger);
        _plugin = plugin;
        _core = core;
        _mouseHook = mouseHook;
        _keyboardHook = keyboardHook;
        _logger = logger;
    }

    public string InputHandlerId => GrabAndMoveConst.InputHandlerId;
    public ushort InputHandlerOrder => GrabAndMoveConst.InputHandlerOrder;
    public bool IsEnableHandler { get; set; } = true;

    public bool SupportKeyDown => true;
    public bool SupportKeyUp => true;
    public bool SupportUp => true;
    public bool SupportDown => true;
    public bool SupportMove => true;
    public bool SupportWhere => false;
    public bool SupportDoubleClick => false;

    public bool IsFeatureEnabled
    {
        get
        {
            lock (_sync)
                return _featureEnabled;
        }
    }

    public void EnableFeature(bool persist = true)
    {
        lock (_sync)
            _featureEnabled = true;

        if (persist)
            _plugin.SetFeatureEnabled(true);

        ReloadSettingsFromPlugin();
        _logger.Information("Grab And Move enabled");
    }

    public void DisableFeature(bool persist = true)
    {
        lock (_sync)
            _featureEnabled = false;

        _core.Reset();

        if (persist)
            _plugin.SetFeatureEnabled(false);

        ReloadSettingsFromPlugin();
        _logger.Information("Grab And Move disabled");
    }

    public void ReloadSettingsFromPlugin()
    {
        var settings = new GrabAndMoveSettings
        {
            Enabled = _plugin.GetFeatureEnabled() != false && IsFeatureEnabled,
            ModifierKey = _plugin.GetActivationModifier() ?? GrabAndMoveModifier.Alt,
            ShouldAbsorbAlt = _plugin.GetPreventAltWindowMenu() != false,
            UseRightClickResize = _plugin.GetRightClickResizeEnabled() != false,
            ExcludedApps = ParseExcludedLines(_plugin.GetExcludedApps()),
            ModifierExcludedApps = ParseExcludedLines(_plugin.GetModifierExcludedApps())
        };

        // When runtime toggle is off, keep settings disabled even if EAV says on.
        lock (_sync)
        {
            if (!_featureEnabled)
                settings.Enabled = false;
        }

        _core.UpdateSettings(settings);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _keyboardHook.StartAsync(cancellationToken).ConfigureAwait(false);
        await _mouseHook.StartAsync(cancellationToken).ConfigureAwait(false);

        // Default SettingEav is true; treat null as enabled.
        if (_plugin.GetFeatureEnabled() != false)
            EnableFeature(persist: false);
        else
            DisableFeature(persist: false);

        _keyboardHook.RegisterHandler(this);
        _mouseHook.RegisterHandler(this);
        _handlersRegistered = true;

        _logger.Information(
            "Grab And Move handlers registered (feature {State})",
            _featureEnabled ? "on" : "off");
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_handlersRegistered)
        {
            _mouseHook.UnregisterHandler(this);
            _keyboardHook.UnregisterHandler(this);
            _handlersRegistered = false;
        }

        _core.Reset();
        _logger.Information("Grab And Move handlers unregistered");
        return Task.CompletedTask;
    }

    public InputEventHandleResult HandleKeyDown(KeyboardEventArgs args)
    {
        if (args.IsSimulator)
            return InputEventHandleResult.None;

        var vk = (uint)(int)args.CurrentKey;
        var disposition = _core.HandleKeyDown(vk, MapScanCode(vk), IsExtended(vk), args.IsSimulator);
        return ToResult(disposition);
    }

    public InputEventHandleResult HandleKeyUp(KeyboardEventArgs args)
    {
        if (args.IsSimulator)
            return InputEventHandleResult.None;

        var disposition = _core.HandleKeyUp((uint)(int)args.CurrentKey, args.IsSimulator);
        return ToResult(disposition);
    }

    public InputEventHandleResult HandleDown(MouseEventArgs args)
    {
        var button = ToButton(args.CurrentKey);
        if (button == MouseButtonKind.None)
            return InputEventHandleResult.None;

        var point = new ScreenPoint((int)args.Position.X, (int)args.Position.Y);
        return ToResult(_core.HandleMouseDown(button, point, args.ExtraInfo));
    }

    public InputEventHandleResult HandleUp(MouseEventArgs args)
    {
        var button = ToButton(args.CurrentKey);
        if (button == MouseButtonKind.None)
            return InputEventHandleResult.None;

        var point = new ScreenPoint((int)args.Position.X, (int)args.Position.Y);
        return ToResult(_core.HandleMouseUp(button, point, args.ExtraInfo));
    }

    public InputEventHandleResult HandleMove(MouseEventArgs args)
    {
        var point = new ScreenPoint((int)args.Position.X, (int)args.Position.Y);
        return ToResult(_core.HandleMouseMove(point, args.ExtraInfo));
    }

    public InputEventHandleResult HandleDoubleClick(MouseEventArgs args) => InputEventHandleResult.None;

    public InputEventHandleResult HandleWheel(MouseEventArgs args) => InputEventHandleResult.None;

    private static InputEventHandleResult ToResult(HookDisposition disposition) => disposition switch
    {
        HookDisposition.Swallow => Consumed,
        HookDisposition.PassWithoutChaining => InputEventHandleResult.Handled,
        _ => InputEventHandleResult.None
    };

    private static MouseButtonKind ToButton(MouseKey key) => key switch
    {
        MouseKey.Left => MouseButtonKind.Left,
        MouseKey.Right => MouseButtonKind.Right,
        _ => MouseButtonKind.None
    };

    private static IReadOnlyList<string> ParseExcludedLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.Length > 0)
            .ToArray();
    }

    private static uint MapScanCode(uint vk)
    {
        const uint mapVkToVsc = 0;
        return MapVirtualKeyW(vk, mapVkToVsc);
    }

    private static bool IsExtended(uint vk) =>
        vk is 0xA5 or 0x5C; // RMENU / RWIN commonly extended

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
}
