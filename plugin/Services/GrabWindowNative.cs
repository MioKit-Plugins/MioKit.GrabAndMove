using System.Runtime.InteropServices;
using System.Text;
using GrabAndMove.Models;

namespace GrabAndMove.Services;

/// <summary>
/// Win32 helpers for window hit-testing and move/resize.
/// Portions adapted from Microsoft PowerToys GrabAndMove (MIT).
/// Copyright (c) Microsoft Corporation.
/// </summary>
internal static class GrabWindowNative
{
    public const int MinWindowWidth = 150;
    public const int MinWindowHeight = 50;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpAsyncWindowPos = 0x4000;
    private const uint GaRoot = 2;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;
    private const int WsThickFrame = 0x00040000;
    private const int SwRestore = 9;
    private const int SmCxDrag = 68;
    private const int SmCyDrag = 69;
    private const int SmRemoteSession = 0x1000;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static bool IsWindow(nint handle) => handle != 0 && IsWindowNative(handle);

    public static bool TryGetWindowRect(nint handle, out WindowRect rect)
    {
        rect = default;
        if (!GetWindowRect(handle, out var native))
            return false;

        rect = new WindowRect(native.Left, native.Top, native.Right, native.Bottom);
        return true;
    }

    public static bool IsZoomed(nint handle) => IsZoomedNative(handle);

    public static void Restore(nint handle) => _ = ShowWindow(handle, SwRestore);

    public static bool HasThickFrame(nint handle)
    {
        var style = IntPtr.Size == 8
            ? GetWindowLongPtr(handle, GwlStyle).ToInt64()
            : GetWindowLong(handle, GwlStyle);
        return (style & WsThickFrame) != 0;
    }

    public static bool MoveWindow(nint handle, WindowRect rect) =>
        SetWindowPos(
            handle,
            0,
            rect.Left,
            rect.Top,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);

    public static bool MoveResizeWindow(nint handle, WindowRect rect) =>
        rect.Width > 0 && rect.Height > 0 && SetWindowPos(
            handle,
            0,
            rect.Left,
            rect.Top,
            rect.Width,
            rect.Height,
            SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);

    public static nint ResolveTargetWindow(ScreenPoint point)
    {
        nint Normalize(nint candidate)
        {
            if (candidate == 0)
                return 0;

            var root = GetAncestor(candidate, GaRoot);
            if (root != 0)
                candidate = root;

            if (candidate == 0 || IsSystemClass(candidate))
                return 0;

            return candidate;
        }

        if (GetSystemMetrics(SmRemoteSession) != 0)
        {
            var foreground = Normalize(GetForegroundWindow());
            if (foreground != 0)
                return foreground;
        }

        var hwnd = Normalize(WindowFromPoint(new NativePoint { X = point.X, Y = point.Y }));
        if (hwnd != 0)
            return hwnd;

        var fg = Normalize(GetForegroundWindow());
        if (fg != 0 && TryGetWindowRect(fg, out var fgRect) &&
            point.X >= fgRect.Left && point.X < fgRect.Right &&
            point.Y >= fgRect.Top && point.Y < fgRect.Bottom)
        {
            return fg;
        }

        return 0;
    }

    public static bool IsSystemClass(nint hwnd)
    {
        var cls = GetClassName(hwnd);
        return cls is
            "Progman" or
            "WorkerW" or
            "Shell_TrayWnd" or
            "Shell_SecondaryTrayWnd" or
            "NotifyIconOverflowWindow" or
            "TopLevelWindowForOverflowXamlIsland" or
            "tooltips_class32" or
            "#32768" or
            "MultitaskingViewFrame" or
            "XamlExplorerHostIslandWindow" or
            "Windows.UI.Composition.DesktopWindowContentBridge" or
            "Shell_InputSwitchTopLevelWindow";
    }

    public static string GetClassName(nint hwnd)
    {
        var builder = new StringBuilder(256);
        _ = GetClassNameW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public static string GetProcessPath(nint hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
            return string.Empty;

        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
            return string.Empty;

        try
        {
            var builder = new StringBuilder(1024);
            var size = (uint)builder.Capacity;
            return QueryFullProcessImageNameW(process, 0, builder, ref size)
                ? builder.ToString()
                : string.Empty;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    public static string GetWindowTitle(nint hwnd)
    {
        var builder = new StringBuilder(512);
        _ = GetWindowTextW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public static int DragThresholdX => Math.Max(1, GetSystemMetrics(SmCxDrag));
    public static int DragThresholdY => Math.Max(1, GetSystemMetrics(SmCyDrag));

    public static nint GetForegroundRoot()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
            return 0;

        var root = GetAncestor(hwnd, GaRoot);
        return root != 0 ? root : hwnd;
    }

    public static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public static bool TryGetCursorPosition(out int x, out int y)
    {
        x = 0;
        y = 0;
        if (!GetCursorPos(out var point))
            return false;

        x = point.X;
        y = point.Y;
        return true;
    }

    public static string GetProcessFileName(nint hwnd)
    {
        var path = GetProcessPath(hwnd);
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFileName(path);
    }

    public static bool TryGetWindowBounds(nint handle, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (!TryGetWindowRect(handle, out var rect))
            return false;

        x = rect.Left;
        y = rect.Top;
        width = rect.Width;
        height = rect.Height;
        return width > 0 && height > 0;
    }

    public static void SetClickThrough(nint handle, bool enabled)
    {
        if (!IsWindow(handle))
            return;

        var style = IntPtr.Size == 8
            ? GetWindowLongPtr(handle, GwlExStyle).ToInt64()
            : GetWindowLong(handle, GwlExStyle);
        var updated = enabled
            ? style | WsExLayered | WsExTransparent
            : style & ~WsExTransparent;

        if (updated == style)
            return;

        if (IntPtr.Size == 8)
            _ = SetWindowLongPtr(handle, GwlExStyle, (nint)updated);
        else
            _ = SetWindowLong(handle, GwlExStyle, unchecked((int)updated));
    }

    public static bool IsShellExperienceExcluded(nint hwnd)
    {
        var cls = GetClassName(hwnd);
        var path = GetProcessPath(hwnd).ToUpperInvariant();

        if (cls == "Windows.UI.Core.CoreWindow")
        {
            return path.Contains("STARTMENUEXPERIENCEHOST.EXE", StringComparison.Ordinal) ||
                   path.Contains("SHELLEXPERIENCEHOST.EXE", StringComparison.Ordinal) ||
                   path.Contains("SEARCHHOST.EXE", StringComparison.Ordinal);
        }

        if (cls == "ControlCenterWindow")
            return path.Contains("SHELLHOST.EXE", StringComparison.Ordinal);

        if (cls == "WindowsDashboard")
            return path.Contains("WIDGETS.EXE", StringComparison.Ordinal) ||
                   path.Contains("WIDGETBOARD.EXE", StringComparison.Ordinal);

        return false;
    }

    public static bool MatchesExcludedApps(nint hwnd, IReadOnlyList<string> apps)
    {
        if (apps.Count == 0)
            return false;

        var path = GetProcessPath(hwnd).ToUpperInvariant();
        var title = GetWindowTitle(hwnd).ToUpperInvariant();

        foreach (var raw in apps)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var app = raw.Trim().ToUpperInvariant();
            if (path.Contains(app, StringComparison.Ordinal) ||
                title.Contains(app, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "IsWindow")]
    private static extern bool IsWindowNative(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", EntryPoint = "IsZoomed")]
    private static extern bool IsZoomedNative(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint value);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(
        nint hProcess,
        uint dwFlags,
        StringBuilder lpExeName,
        ref uint lpdwSize);
}
