using XPadShot.Interop;

namespace XPadShot.Models;

public enum ControllerButton
{
    None,
    A,
    B,
    X,
    Y,
    LB,
    RB,
    LT,
    RT,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Start,
    Back,
    LStick,
    RStick,
}

public static class ControllerButtonExtensions
{
    private const byte TriggerThreshold = 64;

    public static string Label(this ControllerButton button) => button switch
    {
        ControllerButton.None => "None",
        ControllerButton.DPadUp => "D-Pad Up",
        ControllerButton.DPadDown => "D-Pad Down",
        ControllerButton.DPadLeft => "D-Pad Left",
        ControllerButton.DPadRight => "D-Pad Right",
        ControllerButton.LStick => "L3",
        ControllerButton.RStick => "R3",
        _ => button.ToString(),
    };

    public static bool IsPressed(this ControllerButton button, in XInputGamepad pad) => button switch
    {
        ControllerButton.A => (pad.Buttons & 0x1000) != 0,
        ControllerButton.B => (pad.Buttons & 0x2000) != 0,
        ControllerButton.X => (pad.Buttons & 0x4000) != 0,
        ControllerButton.Y => (pad.Buttons & 0x8000) != 0,
        ControllerButton.LB => (pad.Buttons & 0x0100) != 0,
        ControllerButton.RB => (pad.Buttons & 0x0200) != 0,
        ControllerButton.Start => (pad.Buttons & 0x0010) != 0,
        ControllerButton.Back => (pad.Buttons & 0x0020) != 0,
        ControllerButton.DPadUp => (pad.Buttons & 0x0001) != 0,
        ControllerButton.DPadDown => (pad.Buttons & 0x0002) != 0,
        ControllerButton.DPadLeft => (pad.Buttons & 0x0004) != 0,
        ControllerButton.DPadRight => (pad.Buttons & 0x0008) != 0,
        ControllerButton.LStick => (pad.Buttons & 0x0040) != 0,
        ControllerButton.RStick => (pad.Buttons & 0x0080) != 0,
        ControllerButton.LT => pad.LeftTrigger > TriggerThreshold,
        ControllerButton.RT => pad.RightTrigger > TriggerThreshold,
        _ => false,
    };
}
