using System.Runtime.InteropServices;
using GrabAndMove.Models;
using Serilog;

namespace GrabAndMove.Services;

/// <summary>
/// Replays absorbed modifier key / mouse click via SendInput.
/// Adapted from Microsoft PowerToys GrabAndMove (MIT).
/// Copyright (c) Microsoft Corporation.
/// </summary>
internal sealed class ModifierKeyInjector
{
    internal static readonly nuint ReplayTag = 0x47414D56; // "GAMV"

    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;

    private readonly ILogger _logger;

    public ModifierKeyInjector(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public bool TryReplayModifier(CapturedKey key, ModifierReplay replay)
    {
        NativeInput down = CreateKeyInput(key, keyUp: false);
        NativeInput up = CreateKeyInput(key, keyUp: true);

        NativeInput[] inputs = replay switch
        {
            ModifierReplay.DownOnly => [down],
            ModifierReplay.UpOnly => [up],
            ModifierReplay.DownAndUp => [down, up],
            _ => []
        };

        if (inputs.Length == 0)
            return false;

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
        if (sent == inputs.Length)
            return true;

        _logger.Warning(
            "GrabAndMove modifier replay sent {Sent}/{Expected} (Win32 {Error})",
            sent, inputs.Length, Marshal.GetLastWin32Error());
        return false;
    }

    public bool TryReplayClick(MouseButtonKind button, bool down)
    {
        var flags = (button, down) switch
        {
            (MouseButtonKind.Left, true) => MouseEventLeftDown,
            (MouseButtonKind.Left, false) => MouseEventLeftUp,
            (MouseButtonKind.Right, true) => MouseEventRightDown,
            (MouseButtonKind.Right, false) => MouseEventRightUp,
            _ => 0u
        };

        if (flags == 0)
            return false;

        NativeInput[] inputs =
        [
            new NativeInput
            {
                Type = InputMouse,
                Data = new NativeInputUnion
                {
                    Mouse = new NativeMouseInput
                    {
                        Flags = flags,
                        ExtraInfo = ReplayTag
                    }
                }
            }
        ];

        var sent = SendInput(1, inputs, Marshal.SizeOf<NativeInput>());
        return sent == 1;
    }

    public bool TryReplayModifierThenClick(CapturedKey key, MouseButtonKind button)
    {
        NativeInput[] inputs =
        [
            CreateKeyInput(key, keyUp: false),
            new NativeInput
            {
                Type = InputMouse,
                Data = new NativeInputUnion
                {
                    Mouse = new NativeMouseInput
                    {
                        Flags = button == MouseButtonKind.Right ? MouseEventRightDown : MouseEventLeftDown,
                        ExtraInfo = ReplayTag
                    }
                }
            }
        ];

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
        if (sent == inputs.Length)
            return true;

        _logger.Warning(
            "GrabAndMove passthrough replay sent {Sent}/{Expected} (Win32 {Error})",
            sent, inputs.Length, Marshal.GetLastWin32Error());
        return sent > 0;
    }

    private static NativeInput CreateKeyInput(CapturedKey key, bool keyUp)
    {
        uint flags = 0;
        if (key.Extended)
            flags |= KeyEventExtendedKey;
        if (keyUp)
            flags |= KeyEventKeyUp;

        return new NativeInput
        {
            Type = InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new NativeKeyboardInput
                {
                    Vk = (ushort)key.Vk,
                    Scan = (ushort)key.ScanCode,
                    Flags = flags,
                    ExtraInfo = ReplayTag
                }
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeInputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)] public NativeMouseInput Mouse;
        [FieldOffset(0)] public NativeKeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] NativeInput[] inputs, int size);
}
