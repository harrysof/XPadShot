using System.Runtime.InteropServices;

namespace XPadShot.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct XInputGamepad
{
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short ThumbLX;
    public short ThumbLY;
    public short ThumbRX;
    public short ThumbRY;
}

[StructLayout(LayoutKind.Sequential)]
public struct XInputState
{
    public uint PacketNumber;
    public XInputGamepad Gamepad;
}

/// <summary>Minimal XInput binding with a dll fallback chain (1.4 -> 1.3 -> 9.1.0).</summary>
public static class XInput
{
    private delegate uint GetStateDelegate(uint index, ref XInputState state);

    private static readonly GetStateDelegate? GetStateFn = Load();

    public static bool IsAvailable => GetStateFn is not null;

    private static GetStateDelegate? Load()
    {
        foreach (var dll in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
        {
            if (!NativeLibrary.TryLoad(dll, out var handle))
                continue;

            try
            {
                IntPtr export;
                try
                {
                    export = NativeLibrary.GetExport(handle, "XInputGetState");
                }
                catch
                {
                    export = NativeLibrary.GetExport(handle, "#100"); // ordinal export fallback
                }

                return Marshal.GetDelegateForFunctionPointer<GetStateDelegate>(export);
            }
            catch
            {
                // try the next library
            }
        }

        return null;
    }

    /// <summary>Returns true when a controller is connected in the given slot.</summary>
    public static bool IsConnected(uint index, out XInputState state)
    {
        state = default;
        var fn = GetStateFn;
        return fn is not null && fn(index, ref state) == 0;
    }
}
