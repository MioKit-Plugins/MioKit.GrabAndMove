using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GrabAndMove.Services;
using Ti.Avalonia.Shadcn.Controls.Toast;
using Ti.Avalonia.Shadcn.Extensions;

namespace GrabAndMove.Views;

public partial class GrabAndMoveConfigPage : UserControl
{
    private enum PickTarget
    {
        None,
        WindowExclude,
        ModifierExclude
    }

    private PickTarget _pickTarget;
    private nint _hoveredHandle;
    private string? _hoveredProcess;
    private ExcludeAppPickerOverlay? _overlay;

    public GrabAndMoveConfigPage()
    {
        InitializeComponent();
    }

    private GrabAndMoveConfigViewModel? Vm => DataContext as GrabAndMoveConfigViewModel;

    private void WindowExcludePicker_OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        BeginPick(PickTarget.WindowExclude, sender as Control, e);

    private void WindowExcludePicker_OnPointerMoved(object? sender, PointerEventArgs e) =>
        UpdatePickPreview();

    private void WindowExcludePicker_OnPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        EndPick(sender as Control, e, commit: true);

    private void WindowExcludePicker_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        EndPick(null, null, commit: true);

    private void ModifierExcludePicker_OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        BeginPick(PickTarget.ModifierExclude, sender as Control, e);

    private void ModifierExcludePicker_OnPointerMoved(object? sender, PointerEventArgs e) =>
        UpdatePickPreview();

    private void ModifierExcludePicker_OnPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        EndPick(sender as Control, e, commit: true);

    private void ModifierExcludePicker_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        EndPick(null, null, commit: true);

    private void BeginPick(PickTarget target, Control? host, PointerPressedEventArgs e)
    {
        if (Vm is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        e.Pointer.Capture(host);
        _pickTarget = target;
        _hoveredHandle = 0;
        _hoveredProcess = null;

        if (target == PickTarget.WindowExclude)
        {
            Vm.IsPickingWindowExclude = true;
            Vm.SetWindowPickPreview(null);
        }
        else
        {
            Vm.IsPickingModifierExclude = true;
            Vm.SetModifierPickPreview(null);
        }

        EnsureOverlay();
        UpdatePickPreview();
    }

    private void UpdatePickPreview()
    {
        if (_pickTarget == PickTarget.None || Vm is null)
            return;

        if (!GrabWindowNative.TryGetCursorPosition(out var x, out var y))
            return;

        var hwnd = GrabWindowNative.ResolveTargetWindow(new Models.ScreenPoint(x, y));
        var excluded = GetExcludedHandles();
        if (hwnd == 0 || excluded.Contains(hwnd))
        {
            _hoveredHandle = 0;
            _hoveredProcess = null;
            _overlay?.Hide();
            if (_pickTarget == PickTarget.WindowExclude)
                Vm.SetWindowPickPreview(null);
            else
                Vm.SetModifierPickPreview(null);
            return;
        }

        var process = GrabWindowNative.GetProcessFileName(hwnd);
        var title = GrabWindowNative.GetWindowTitle(hwnd);
        _hoveredHandle = hwnd;
        _hoveredProcess = process;

        if (_pickTarget == PickTarget.WindowExclude)
            Vm.SetWindowPickPreview(process);
        else
            Vm.SetModifierPickPreview(process);

        if (GrabWindowNative.TryGetWindowBounds(hwnd, out var bx, out var by, out var bw, out var bh))
            _overlay?.MoveTo(bx, by, bw, bh, title, process);
    }

    private void EndPick(Control? host, PointerReleasedEventArgs? e, bool commit)
    {
        if (_pickTarget == PickTarget.None)
            return;

        var target = _pickTarget;
        var process = _hoveredProcess;
        _pickTarget = PickTarget.None;
        _hoveredHandle = 0;
        _hoveredProcess = null;

        e?.Pointer.Capture(null);
        host?.ClearValue(InputElement.CursorProperty);
        _overlay?.Hide();

        if (Vm is not null)
        {
            Vm.IsPickingWindowExclude = false;
            Vm.IsPickingModifierExclude = false;
            Vm.SetWindowPickPreview(null);
            Vm.SetModifierPickPreview(null);
        }

        if (!commit || string.IsNullOrWhiteSpace(process) || Vm is null)
            return;

        if (target == PickTarget.WindowExclude)
            Vm.AppendWindowExcludeProcess(process);
        else
            Vm.AppendModifierExcludeProcess(process);

        Vm.Window?.GetShadToastManager()
            .CreateToast()
            .WithBasicSuccess($"已加入草稿：{process}")
            .Queue();
    }

    private void EnsureOverlay()
    {
        if (_overlay is not null)
            return;

        _overlay = new ExcludeAppPickerOverlay();
        WindowManager.SetOwnerId(_overlay, GrabAndMoveConst.PluginId);
        _overlay.Opened += (_, _) =>
        {
            var handle = TryGetHwnd(_overlay);
            if (handle != 0)
                GrabWindowNative.SetClickThrough(handle, enabled: true);
        };
    }

    private IReadOnlyCollection<nint> GetExcludedHandles()
    {
        List<nint> handles = [];
        var window = TopLevel.GetTopLevel(this) as Window;
        var panel = TryGetHwnd(window);
        if (panel != 0)
            handles.Add(panel);
        var overlay = TryGetHwnd(_overlay);
        if (overlay != 0)
            handles.Add(overlay);
        return handles;
    }

    private static nint TryGetHwnd(Window? window) =>
        window?.TryGetPlatformHandle()?.Handle ?? 0;

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _overlay?.Close();
        _overlay = null;
        base.OnDetachedFromVisualTree(e);
    }
}
