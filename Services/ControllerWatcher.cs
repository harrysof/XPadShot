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
    private int _first = (int)ControllerButton.LB;
    private int _second = (int)ControllerButton.A;
    private int _recordFirst = (int)ControllerButton.RB;
    private int _recordSecond = (int)ControllerButton.B;
    private int _currentSlot = -2; // -2 = not polled yet

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
            int first = (int)First;
            int second = (int)Second;
            int recordFirst = (int)RecordFirst;
            int recordSecond = (int)RecordSecond;

            // A combo swap (e.g. a profile switch) must not fire a stale
            // trigger: restart edge detection when a pair changes.
            if (first != seenFirst || second != seenSecond)
            {
                seenFirst = first;
                seenSecond = second;
                wasHeld = false;
            }

            if (recordFirst != seenRecordFirst || recordSecond != seenRecordSecond)
            {
                seenRecordFirst = recordFirst;
                seenRecordSecond = recordSecond;
                wasRecordHeld = false;
            }

            int slot = FindConnectedSlot(currentSlot, out var state);

            if (slot != currentSlot)
            {
                currentSlot = slot;
                wasHeld = false;
                wasRecordHeld = false;
                Volatile.Write(ref _currentSlot, slot);
                ConnectionChanged?.Invoke(slot);
            }

            bool held = slot >= 0
                && First.IsPressed(state.Gamepad)
                && (Second == ControllerButton.None || Second.IsPressed(state.Gamepad));

            if (held && !wasHeld)
                ComboTriggered?.Invoke();

            if (held != wasHeld)
            {
                wasHeld = held;
                ComboHeldChanged?.Invoke(held);
            }

            bool recordHeld = slot >= 0
                && RecordFirst.IsPressed(state.Gamepad)
                && (RecordSecond == ControllerButton.None || RecordSecond.IsPressed(state.Gamepad));

            if (recordHeld && !wasRecordHeld)
                RecordTriggered?.Invoke();

            wasRecordHeld = recordHeld;

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static int FindConnectedSlot(int preferred, out XInputState state)
    {
        if (preferred >= 0 && XInput.IsConnected((uint)preferred, out state))
            return preferred;

        for (uint i = 0; i < 4; i++)
        {
            if (XInput.IsConnected(i, out state))
                return (int)i;
        }

        state = default;
        return -1;
    }

    public void Dispose() => _running = false;
}
