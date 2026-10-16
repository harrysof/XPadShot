using System.Text.Json.Serialization;

namespace XPadShot.Models;

/// <summary>A named combo profile. Profiles are free-form - they are not bound
/// to any process; the user names them and switches between them.
/// Each profile carries a screenshot combo and a screen-recording combo
/// (which synthesizes a keyboard shortcut).</summary>
public sealed record GameProfile
{
    public string Name { get; init; } = "";
    public ControllerButton First { get; init; } = ControllerButton.LB;
    public ControllerButton Second { get; init; } = ControllerButton.A;
    public ControllerButton RecordFirst { get; init; } = ControllerButton.RB;
    public ControllerButton RecordSecond { get; init; } = ControllerButton.B;
    public RecordShortcut RecordShortcut { get; init; } = RecordShortcut.GameBarRecord;

    /// <summary>Id of the CustomShortcut used when RecordShortcut is Custom.</summary>
    public string? RecordShortcutId { get; init; }

    // Legacy (pre-1.7): the custom keys lived directly on the profile.
    // Migration extracts them into a named CustomShortcut; only read.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ShortcutModifiers RecordCustomModifiers { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RecordCustomKey { get; init; }
}
