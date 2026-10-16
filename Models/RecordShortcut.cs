using CommunityToolkit.Mvvm.ComponentModel;

namespace XPadShot.Models;

/// <summary>How a profile triggers screen recording.
/// GameBarOpen and SnippingRecord are retired values from older settings files;
/// settings migration rewrites them to GameBarRecord.</summary>
public enum RecordShortcut
{
    GameBarRecord = 0,
    Custom = 1,
    GameBarOpen = 2,
    SnippingRecord = 3,
}

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>A user-named keyboard shortcut from the custom-shortcut library.</summary>
public sealed record CustomShortcut
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ShortcutModifiers Modifiers { get; init; }
    public int VirtualKey { get; init; }
}

/// <summary>An entry of the recording-shortcut dropdown (built-in or custom).</summary>
public sealed partial class RecordShortcutOption : ObservableObject
{
    public const string GameBarKey = "preset:gamebar";

    [ObservableProperty]
    private string _display;

    public RecordShortcutOption(string key, string display)
    {
        Key = key;
        _display = display;
    }

    /// <summary>Dropdown identity: "preset:gamebar" or "custom:&lt;id&gt;".</summary>
    public string Key { get; }

    public static string ForCustom(string id) => "custom:" + id;
}

/// <summary>Formats key combos for display ("Ctrl + Shift + F9").</summary>
public static class ShortcutText
{
    public const string GameBarRecordKeys = "Win + Alt + R";
    public const string GameBarRecordDisplay = $"Game Bar record ({GameBarRecordKeys})";

    public static string Describe(ShortcutModifiers modifiers, int virtualKey)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ShortcutModifiers.Win))
            parts.Add("Win");
        if (modifiers.HasFlag(ShortcutModifiers.Ctrl))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(ShortcutModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(ShortcutModifiers.Shift))
            parts.Add("Shift");

        var key = ShortcutKeyCatalog.DisplayName(virtualKey);
        if (key.Length > 0)
            parts.Add(key);

        return parts.Count == 0 ? "not set" : string.Join(" + ", parts);
    }
}
