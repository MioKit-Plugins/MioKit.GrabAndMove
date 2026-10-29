using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrabAndMove.Models;
using GrabAndMove.Services;
using Ti.Avalonia.Shadcn.Controls;
using Ti.Avalonia.Shadcn.Controls.Toast;
using Ti.Avalonia.Shadcn.Extensions;

namespace GrabAndMove.Views;

internal partial class GrabAndMoveConfigViewModel : ObservableObject
{
    private readonly GrabAndMovePlugin _plugin;
    private readonly GrabAndMoveService _service;
    private bool _isLoaded;
    private string _appliedExcludedApps = string.Empty;
    private string _appliedModifierExcludedApps = string.Empty;

    public GrabAndMoveConfigViewModel()
    {
        _plugin = null!;
        _service = null!;
        _isLoaded = false;
    }

    public GrabAndMoveConfigViewModel(GrabAndMovePlugin plugin, GrabAndMoveService service)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(service);
        _plugin = plugin;
        _service = service;
    }

    /// <summary>Preview / design shell without plugin DI.</summary>
    public static GrabAndMoveConfigViewModel CreateDesignPreview() => new();

    public ShadWindow? Window { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(GestureCardsOpacity))]
    [NotifyPropertyChangedFor(nameof(ResizeHintOpacity))]
    private bool _isFeatureEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAltSelected))]
    [NotifyPropertyChangedFor(nameof(IsWinSelected))]
    [NotifyPropertyChangedFor(nameof(AltMenuSettingOpacity))]
    [NotifyPropertyChangedFor(nameof(ModifierDisplayName))]
    [NotifyPropertyChangedFor(nameof(MoveGestureHint))]
    [NotifyPropertyChangedFor(nameof(ResizeGestureHint))]
    private GrabAndMoveModifier _activationModifier = GrabAndMoveModifier.Alt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResizeGestureHint))]
    [NotifyPropertyChangedFor(nameof(ResizeHintOpacity))]
    private bool _rightClickResizeEnabled = true;

    [ObservableProperty]
    private bool _preventAltWindowMenu = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnappliedExcludeChanges))]
    [NotifyPropertyChangedFor(nameof(UnappliedChangesOpacity))]
    [NotifyCanExecuteChangedFor(nameof(ApplyExclusionsCommand))]
    private string _excludedAppsDraft = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnappliedExcludeChanges))]
    [NotifyPropertyChangedFor(nameof(UnappliedChangesOpacity))]
    [NotifyCanExecuteChangedFor(nameof(ApplyExclusionsCommand))]
    private string _modifierExcludedAppsDraft = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowPickHintOpacity))]
    private bool _isPickingWindowExclude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierPickHintOpacity))]
    private bool _isPickingModifierExclude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowPickHintOpacity))]
    private string _windowPickHint = "拖到窗口后松开";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModifierPickHintOpacity))]
    private string _modifierPickHint = "拖到窗口后松开";

    public bool IsRunning => IsFeatureEnabled;
    public string StatusText => IsFeatureEnabled ? "运行中" : "已暂停";
    public double GestureCardsOpacity => IsFeatureEnabled ? 1 : 0.45;
    public bool IsAltSelected => ActivationModifier == GrabAndMoveModifier.Alt;
    public bool IsWinSelected => ActivationModifier == GrabAndMoveModifier.Win;
    public double AltMenuSettingOpacity => IsAltSelected ? 1 : 0.45;
    public string ModifierDisplayName => ActivationModifier == GrabAndMoveModifier.Win ? "Win" : "Alt";
    public string MoveGestureHint => $"{ModifierDisplayName} + 左键拖动";
    public string ResizeGestureHint => RightClickResizeEnabled
        ? $"{ModifierDisplayName} + 右键拖动"
        : "已关闭";
    public double ResizeHintOpacity =>
        !RightClickResizeEnabled || !IsFeatureEnabled ? 0.45 : 1;

    public bool HasUnappliedExcludeChanges =>
        !string.Equals(NormalizeLines(ExcludedAppsDraft), NormalizeLines(_appliedExcludedApps), StringComparison.Ordinal) ||
        !string.Equals(
            NormalizeLines(ModifierExcludedAppsDraft),
            NormalizeLines(_appliedModifierExcludedApps),
            StringComparison.Ordinal);

    public double UnappliedChangesOpacity => HasUnappliedExcludeChanges ? 1 : 0;
    public double WindowPickHintOpacity => IsPickingWindowExclude ? 1 : 0;
    public double ModifierPickHintOpacity => IsPickingModifierExclude ? 1 : 0;

    public void LoadFromPlugin()
    {
        _isLoaded = false;

        IsFeatureEnabled = _service.IsFeatureEnabled;
        ActivationModifier = _plugin.GetActivationModifier() ?? GrabAndMoveModifier.Alt;
        RightClickResizeEnabled = _plugin.GetRightClickResizeEnabled() != false;
        PreventAltWindowMenu = _plugin.GetPreventAltWindowMenu() != false;

        _appliedExcludedApps = _plugin.GetExcludedApps() ?? string.Empty;
        _appliedModifierExcludedApps = _plugin.GetModifierExcludedApps() ?? string.Empty;
        ExcludedAppsDraft = _appliedExcludedApps;
        ModifierExcludedAppsDraft = _appliedModifierExcludedApps;

        _isLoaded = true;
        OnPropertyChanged(nameof(HasUnappliedExcludeChanges));
        OnPropertyChanged(nameof(UnappliedChangesOpacity));
        ApplyExclusionsCommand.NotifyCanExecuteChanged();
    }

    public void AppendWindowExcludeProcess(string? processFileName)
    {
        ExcludedAppsDraft = AppendLine(ExcludedAppsDraft, processFileName);
    }

    public void AppendModifierExcludeProcess(string? processFileName)
    {
        ModifierExcludedAppsDraft = AppendLine(ModifierExcludedAppsDraft, processFileName);
    }

    public void SetWindowPickPreview(string? processFileName)
    {
        WindowPickHint = string.IsNullOrWhiteSpace(processFileName)
            ? "拖到窗口后松开"
            : $"松开以添加 {processFileName}";
    }

    public void SetModifierPickPreview(string? processFileName)
    {
        ModifierPickHint = string.IsNullOrWhiteSpace(processFileName)
            ? "拖到窗口后松开"
            : $"松开以添加 {processFileName}";
    }

    partial void OnIsFeatureEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        if (value)
            _service.EnableFeature();
        else
            _service.DisableFeature();
    }

    partial void OnActivationModifierChanged(GrabAndMoveModifier value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetActivationModifier(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnRightClickResizeEnabledChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetRightClickResizeEnabled(value);
        _service.ReloadSettingsFromPlugin();
    }

    partial void OnPreventAltWindowMenuChanged(bool value)
    {
        if (!_isLoaded)
            return;

        _plugin.SetPreventAltWindowMenu(value);
        _service.ReloadSettingsFromPlugin();
    }

    [RelayCommand]
    private void SelectAltModifier()
    {
        ActivationModifier = GrabAndMoveModifier.Alt;
    }

    [RelayCommand]
    private void SelectWinModifier()
    {
        ActivationModifier = GrabAndMoveModifier.Win;
    }

    [RelayCommand(CanExecute = nameof(HasUnappliedExcludeChanges))]
    private void ApplyExclusions()
    {
        var excluded = NormalizeLines(ExcludedAppsDraft);
        var modifierExcluded = NormalizeLines(ModifierExcludedAppsDraft);

        ExcludedAppsDraft = excluded;
        ModifierExcludedAppsDraft = modifierExcluded;
        _appliedExcludedApps = excluded;
        _appliedModifierExcludedApps = modifierExcluded;

        _plugin.SetExcludedApps(excluded);
        _plugin.SetModifierExcludedApps(modifierExcluded);
        _service.ReloadSettingsFromPlugin();

        OnPropertyChanged(nameof(HasUnappliedExcludeChanges));
        OnPropertyChanged(nameof(UnappliedChangesOpacity));
        ApplyExclusionsCommand.NotifyCanExecuteChanged();

        Window?.GetShadToastManager()
            .CreateToast()
            .WithBasicSuccess("已更新排除列表")
            .Queue();
    }

    private static string AppendLine(string current, string? chip)
    {
        if (string.IsNullOrWhiteSpace(chip))
            return current;

        var lines = (current ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (lines.Any(line => string.Equals(line, chip, StringComparison.OrdinalIgnoreCase)))
            return string.Join(Environment.NewLine, lines);

        lines.Add(chip.Trim());
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
