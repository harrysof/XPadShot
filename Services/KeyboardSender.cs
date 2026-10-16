using System.Runtime.InteropServices;
using XPadShot.Models;

namespace XPadShot.Services;

/// <summary>
/// Synthesizes recording keyboard shortcuts. Verified on this machine:
/// injected Win-combos reach the Xbox Game Bar hotkey (Win+Alt+R starts/stops
/// a recording that lands in Videos\Captures).
/// </summary>
public static class KeyboardSender
{
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_ALT = 0x12;
    private const ushort VK_CTRL = 0x11;
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_R = 0x52;

    /// <summary>Win + Alt + R - the Game Bar "record that" hotkey.</summary>
    public static void SendGameBarRecord() => SendChord(VK_LWIN, VK_ALT, VK_R);

    /// <summary>Sends a custom shortcut: hold the modifiers, press the key.</summary>
    public static void SendCustom(ShortcutModifiers modifiers, int virtualKey)
    {
        if (virtualKey == 0)
            return;

        var keys = new List<ushort>();

        if (modifiers.HasFlag(ShortcutModifiers.Win))
            keys.Add(VK_LWIN);
        if (modifiers.HasFlag(ShortcutModifiers.Ctrl))
            keys.Add(VK_CTRL);
        if (modifiers.HasFlag(ShortcutModifiers.Alt))
            keys.Add(VK_ALT);
        if (modifiers.HasFlag(ShortcutModifiers.Shift))
            keys.Add(VK_SHIFT);

        keys.Add((ushort)virtualKey);
        SendChord([.. keys]);
    }

    private static void SendChord(params ushort[] keys)
    {
        foreach (var key in keys)
            KeyEvent(key, up: false);

        for (int i = keys.Length - 1; i >= 0; i--)
            KeyEvent(keys[i], up: true);
    }

    private static void KeyEvent(ushort vk, bool up)
    {
        var inputs = new INPUT[1];
        inputs[0].type = 1; // KEYBOARD_INPUT
        inputs[0].u.ki.wVk = vk;
        inputs[0].u.ki.dwFlags = up ? 2u : 0u;
        _ = SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        Thread.Sleep(50);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
