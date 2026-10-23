using XPadShot.Interop;
using XPadShot.Models;

namespace XPadShot.Services;

/// <summary>
/// Polls XInput in the background and reports controller presence,
/// live combo state, and combo press edges.
/// </summary>
public sealed class ControllerWatcher : IDisposable
{
    private const int PollIntervalMs = 8;

    private readonly Thread _thread;
    private volatile bool _running = true;
    private volatile bool _enabled = true;
    private int _first = (int)ControllerButton.LB;
    private int _second = (int)ControllerButton.A;
    private int _recordFirst = (int)ControllerButton.RB;
    private int _recordSecond = (int)ControllerButton.B;
    private int _pinnedSlot;
    private int _currentSlot = -2; // -2 = not polled yet

    /// <summary>Master switch for the capture/record combos: when false, they
    /// never trigger, but the pad is still tracked for the player picker.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>XInput position to listen on (0-3). Player N uses N-1.</summary>
    public int PinnedSlot
    {
        get => Volatile.Read(ref _pinnedSlot);
        set => Volatile.Write(ref _pinnedSlot, Math.Clamp(value, 0, 3));
    }

    /// <summary>Latest known slot: 0-3, -1 for none, -2 before the first poll.</summary>
    public int CurrentSlot => Volatile.Read(ref _currentSlot);

    /// <summary>Slot index (0-3), or -1 when no controller is connected.</summary>
    public event Action<int>? ConnectionChanged;

    /// <summary>True while both combo buttons are held.</summary>
    public event Action<bool>? ComboHeldChanged;

    /// <summary>Fires once per press (on the rising edge of the combo).</summary>
    public event Action? ComboTriggered;

    /// <summary>Fires once per press of the screen-recording combo.</summary>
    public event Action? RecordTriggered;

    public ControllerWatcher()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "xinput-poll" };
        _thread.Start();
    }

    public ControllerButton First
    {
        get => (ControllerButton)Volatile.Read(ref _first);
        set => Volatile.Write(ref _first, (int)value);
    }

    public ControllerButton Second
    {
        get => (ControllerButton)Volatile.Read(ref _second);
        set => Volatile.Write(ref _second, (int)value);
    }

    public ControllerButton RecordFirst
    {
        get => (ControllerButton)Volatile.Read(ref _recordFirst);
        set => Volatile.Write(ref _recordFirst, (int)value);
    }

    public ControllerButton RecordSecond
    {
        get => (ControllerButton)Volatile.Read(ref _recordSecond);
        set => Volatile.Write(ref _recordSecond, (int)value);
    }

    private void Loop()
    {
        int currentSlot = -2; // sentinel: guarantees an initial ConnectionChanged
        bool wasHeld = false;
        bool wasRecordHeld = false;
        int seenFirst = -1;
        int seenSecond = -1;
        int seenRecordFirst = -1;
        int seenRecordSecond = -1;

        while (_running)
        {
            bool enabled = Enabled;
            int first = (int)First;
            int second = (int)Second;
            int recordFirst = (int)RecordFirst;
            int recordSecond = (int)RecordSecond;

            // A combo swap (e.g. a profile switch) must not fire a stale
            // trigger: restart edge detection when a pair changes.
            if (first != seenFirst || second != seenSecond)
            {
                bool wasHeldBeforeSwap = wasHeld;
                seenFirst = first;
                seenSecond = second;
                wasHeld = false;

                if (wasHeldBeforeSwap)
                    ComboHeldChanged?.Invoke(false);
            }

            if (recordFirst != seenRecordFirst || recordSecond != seenRecordSecond)
            {
                seenRecordFirst = recordFirst;
                seenRecordSecond = recordSecond;
                wasRecordHeld = false;
            }

            int slot = FindConnectedSlot(PinnedSlot, out var state);

            if (slot != currentSlot)
            {
                currentSlot = slot;
                wasHeld = false;
                wasRecordHeld = false;
                Volatile.Write(ref _currentSlot, slot);
                ConnectionChanged?.Invoke(slot);
            }

            var firstButton = (ControllerButton)first;
            var secondButton = (ControllerButton)second;
            bool hasCombo = firstButton != ControllerButton.None
                || secondButton != ControllerButton.None;

            bool held = enabled
                && slot >= 0
                && hasCombo
                && (firstButton == ControllerButton.None || firstButton.IsPressed(state.Gamepad))
                && (secondButton == ControllerButton.None || secondButton.IsPressed(state.Gamepad));

            if (held && !wasHeld)
                ComboTriggered?.Invoke();

            if (held != wasHeld)
            {
                wasHeld = held;
                ComboHeldChanged?.Invoke(held);
            }

            var recordFirstButton = (ControllerButton)recordFirst;
            var recordSecondButton = (ControllerButton)recordSecond;
            bool hasRecordCombo = recordFirstButton != ControllerButton.None
                || recordSecondButton != ControllerButton.None;

            bool recordHeld = enabled
                && slot >= 0
                && hasRecordCombo
                && (recordFirstButton == ControllerButton.None || recordFirstButton.IsPressed(state.Gamepad))
                && (recordSecondButton == ControllerButton.None || recordSecondButton.IsPressed(state.Gamepad));

            if (recordHeld && !wasRecordHeld)
                RecordTriggered?.Invoke();

            wasRecordHeld = recordHeld;

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static int FindConnectedSlot(int pinned, out XInputState state)
    {
        if (XInput.IsConnected((uint)pinned, out state))
            return pinned;

        state = default;
        return -1;
    }

    public void Dispose() => _running = false;
}
