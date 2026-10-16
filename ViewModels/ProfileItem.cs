using CommunityToolkit.Mvvm.ComponentModel;
using XPadShot.Models;

namespace XPadShot.ViewModels;

/// <summary>A named, editable profile: screenshot combo + recording combo.</summary>
public sealed partial class ProfileItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private ControllerButton _first;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private ControllerButton _second;

    [ObservableProperty]
    private ControllerButton _recordFirst;

    [ObservableProperty]
    private ControllerButton _recordSecond;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordOptionKey))]
    private RecordShortcut _recordShortcut;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordOptionKey))]
    private string? _recordShortcutId;

    public ProfileItem(
        string name,
        ControllerButton first,
        ControllerButton second,
        ControllerButton recordFirst = ControllerButton.RB,
        ControllerButton recordSecond = ControllerButton.B,
        RecordShortcut recordShortcut = RecordShortcut.GameBarRecord,
        string? recordShortcutId = null)
    {
        _name = name;
        _first = first;
        _second = second;
        _recordFirst = recordFirst;
        _recordSecond = recordSecond;
        _recordShortcut = recordShortcut;
        _recordShortcutId = recordShortcutId;
    }

    public string Display => Second == ControllerButton.None
        ? $"{Name} - {First.Label()}"
        : $"{Name} - {First.Label()} + {Second.Label()}";

    /// <summary>Recording-shortcut dropdown key: the built-in Game Bar entry or
    /// a custom shortcut ("custom:&lt;id&gt;"). Bound to the dropdown's value.</summary>
    public string RecordOptionKey
    {
        get => RecordShortcut == RecordShortcut.Custom && !string.IsNullOrEmpty(RecordShortcutId)
            ? RecordShortcutOption.ForCustom(RecordShortcutId)
            : RecordShortcutOption.GameBarKey;
        set
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (value.StartsWith("custom:", StringComparison.Ordinal))
            {
                RecordShortcutId = value["custom:".Length..];
                RecordShortcut = RecordShortcut.Custom;
            }
            else
            {
                RecordShortcutId = null;
                RecordShortcut = RecordShortcut.GameBarRecord;
            }
        }
    }

    public GameProfile ToProfile() => new()
    {
        Name = Name,
        First = First,
        Second = Second,
        RecordFirst = RecordFirst,
        RecordSecond = RecordSecond,
        RecordShortcut = RecordShortcut,
        RecordShortcutId = RecordShortcutId,
    };
}
