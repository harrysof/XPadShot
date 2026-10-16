using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using XPadShot.Models;
using XPadShot.Services;

namespace XPadShot.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ControllerWatcher _watcher;
    private readonly ForegroundWatcher _foreground;
    private readonly SettingsService _settings;
    private readonly ScreenshotService _screenshots;
    private readonly SoundService _sound;

    private int _captureBusy;
    private int _recordBusy;
    private int _captureCount;
    private bool _recordingActive;

    /// <summary>Raised when a capture completed (drives the LED flash).</summary>
    public event Action? CaptureFlash;

    public string FolderPath { get; }

    /// <summary>All buttons, for the first combo picker.</summary>
    public IReadOnlyList<ButtonOption> FirstOptions { get; }

    /// <summary>Named custom recording shortcuts, shared across profiles.</summary>
    public ObservableCollection<CustomShortcut> CustomShortcuts { get; } = [];

    /// <summary>Entries of the recording-shortcut dropdown (built-in + customs).</summary>
    public ObservableCollection<RecordShortcutOption> RecordShortcutOptions { get; } = [];

    public ObservableCollection<ProfileItem> Profiles { get; } = [];

    [ObservableProperty] private ProfileItem? _selectedProfile;
    [ObservableProperty] private IReadOnlyList<ButtonOption> _secondOptions = [];
    [ObservableProperty] private IReadOnlyList<ButtonOption> _recordSecondOptions = [];
    [ObservableProperty] private bool _canRemoveProfile;
    [ObservableProperty] private bool _canEditShortcut;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renameDraft = "";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionText = "Looking for a controller...";
    [ObservableProperty] private string _connectionHint = "";
    [ObservableProperty] private bool _isComboHeld;
    [ObservableProperty] private string _stateWord = "Ready";
    [ObservableProperty] private string _stateDetail = "";
    [ObservableProperty] private string _sessionText = "";

    public MainViewModel(
        ControllerWatcher watcher,
        ForegroundWatcher foreground,
        SettingsService settings,
        ScreenshotService screenshots,
        SoundService sound)
    {
        _watcher = watcher;
        _foreground = foreground;
        _settings = settings;
        _screenshots = screenshots;
        _sound = sound;
        FolderPath = screenshots.FolderPath;

        FirstOptions = Enum.GetValues<ControllerButton>()
            .Where(b => b != ControllerButton.None)
            .Select(b => new ButtonOption(b, b.Label()))
            .ToList();

        var saved = settings.Load();

        foreach (var shortcut in saved.CustomShortcuts)
            CustomShortcuts.Add(shortcut);

        foreach (var profile in saved.Profiles)
            Profiles.Add(new ProfileItem(
                profile.Name,
                profile.First,
                profile.Second,
                profile.RecordFirst,
                profile.RecordSecond,
                profile.RecordShortcut,
                profile.RecordShortcutId));

        // Safety net; the first-run dialog normally creates the initial profile.
        if (Profiles.Count == 0)
            Profiles.Add(new ProfileItem("Default", ControllerButton.LB, ControllerButton.A));

        foreach (var item in Profiles)
            Hook(item);

        _selectedProfile = Profiles.FirstOrDefault(p => p.Name == saved.SelectedProfile) ?? Profiles[0];
        _startWithWindows = StartupService.IsEnabled();
        RebuildSecondOptions();
        RebuildRecordSecondOptions();
        RecordShortcutOptions.Add(new RecordShortcutOption(RecordShortcutOption.GameBarKey, ShortcutText.GameBarRecordDisplay));
        SyncRecordShortcutOptions();
        UpdateShortcutButtons();
        SyncWatcher();
        CanRemoveProfile = Profiles.Count > 1;

        _watcher.ConnectionChanged += slot => OnUi(() => ApplyConnection(slot));
        _watcher.ComboHeldChanged += held => OnUi(() => ApplyComboHeld(held));
        _watcher.ComboTriggered += () => OnUi(TriggerCapture);
        _watcher.RecordTriggered += () => OnUi(TriggerRecording);

        // Close the startup race: a poll may complete before we subscribe.
        if (_watcher.CurrentSlot != -2)
            ApplyConnection(_watcher.CurrentSlot);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _foreground.Dispose();
    }

    partial void OnSelectedProfileChanged(ProfileItem? value)
    {
        RebuildSecondOptions();
        RebuildRecordSecondOptions();
        UpdateShortcutButtons();
        SyncWatcher();
        Persist();
    }

    partial void OnStartWithWindowsChanged(bool value) => StartupService.SetEnabled(value);

    private void Hook(ProfileItem item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileItem.Name))
            {
                Persist();
                return;
            }

            if (e.PropertyName == nameof(ProfileItem.First) && item.Second == item.First)
                item.Second = ControllerButton.None;

            if (e.PropertyName == nameof(ProfileItem.RecordFirst) && item.RecordSecond == item.RecordFirst)
                item.RecordSecond = ControllerButton.None;

            if (e.PropertyName is nameof(ProfileItem.First) or nameof(ProfileItem.Second)
                or nameof(ProfileItem.RecordFirst) or nameof(ProfileItem.RecordSecond)
                or nameof(ProfileItem.RecordShortcut) or nameof(ProfileItem.RecordShortcutId))
            {
                Persist();

                if (ReferenceEquals(item, SelectedProfile))
                {
                    if (e.PropertyName == nameof(ProfileItem.First))
                        RebuildSecondOptions();
                    if (e.PropertyName == nameof(ProfileItem.RecordFirst))
                        RebuildRecordSecondOptions();
                    if (e.PropertyName is nameof(ProfileItem.RecordShortcut) or nameof(ProfileItem.RecordShortcutId))
                        UpdateShortcutButtons();

                    SyncWatcher();
                }
            }
        };
    }

    private void RebuildSecondOptions()
    {
        var first = SelectedProfile?.First ?? ControllerButton.LB;

        SecondOptions =
        [
            new ButtonOption(ControllerButton.None, "None"),
            .. FirstOptions.Where(o => o.Value != first),
        ];
    }

    private void RebuildRecordSecondOptions()
    {
        var recordFirst = SelectedProfile?.RecordFirst ?? ControllerButton.RB;

        RecordSecondOptions =
        [
            new ButtonOption(ControllerButton.None, "None"),
            .. FirstOptions.Where(o => o.Value != recordFirst),
        ];
    }

    /// <summary>Keeps the dropdown in sync with the custom-shortcut library,
    /// updating entries in place so the current selection survives.</summary>
    private void SyncRecordShortcutOptions()
    {
        foreach (var shortcut in CustomShortcuts)
        {
            if (!RecordShortcutOptions.Any(o => o.Key == RecordShortcutOption.ForCustom(shortcut.Id)))
                RecordShortcutOptions.Add(MakeOption(shortcut));
        }

        for (var i = RecordShortcutOptions.Count - 1; i >= 0; i--)
        {
            var option = RecordShortcutOptions[i];
            if (option.Key == RecordShortcutOption.GameBarKey)
                continue;

            var shortcut = CustomShortcuts.FirstOrDefault(c => RecordShortcutOption.ForCustom(c.Id) == option.Key);
            if (shortcut is null)
                RecordShortcutOptions.RemoveAt(i);
            else
                option.Display = CustomDisplay(shortcut);
        }
    }

    private static RecordShortcutOption MakeOption(CustomShortcut shortcut) =>
        new(RecordShortcutOption.ForCustom(shortcut.Id), CustomDisplay(shortcut));

    private static string CustomDisplay(CustomShortcut shortcut) =>
        $"{shortcut.Name} ({ShortcutText.Describe(shortcut.Modifiers, shortcut.VirtualKey)})";

    /// <summary>Adds a custom shortcut and selects it for the current profile.</summary>
    public void AddCustomShortcut(string name, ShortcutModifiers modifiers, int virtualKey)
    {
        var shortcut = new CustomShortcut
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = NormalizeName(name),
            Modifiers = modifiers,
            VirtualKey = virtualKey,
        };

        CustomShortcuts.Add(shortcut);
        SyncRecordShortcutOptions();

        if (SelectedProfile is { } profile)
            profile.RecordOptionKey = RecordShortcutOption.ForCustom(shortcut.Id); // the hook persists
        else
            Persist();
    }

    public void UpdateCustomShortcut(string id, string name, ShortcutModifiers modifiers, int virtualKey)
    {
        var index = IndexOfCustom(id);
        if (index < 0)
            return;

        CustomShortcuts[index] = CustomShortcuts[index] with
        {
            Name = NormalizeName(name),
            Modifiers = modifiers,
            VirtualKey = virtualKey,
        };

        SyncRecordShortcutOptions();
        Persist();
    }

    /// <summary>Deletes a custom shortcut; profiles that used it fall back to the Game Bar record.</summary>
    public void DeleteCustomShortcut(string id)
    {
        var index = IndexOfCustom(id);
        if (index < 0)
            return;

        CustomShortcuts.RemoveAt(index);
        SyncRecordShortcutOptions();

        foreach (var profile in Profiles.Where(p =>
                     p.RecordShortcut == RecordShortcut.Custom && p.RecordShortcutId == id))
        {
            profile.RecordShortcut = RecordShortcut.GameBarRecord;
            profile.RecordShortcutId = null; // the hook persists + refreshes the buttons
        }

        Persist();
    }

    public CustomShortcut? FindCustomShortcut(string? id) =>
        id is null ? null : CustomShortcuts.FirstOrDefault(c => c.Id == id);

    private int IndexOfCustom(string id)
    {
        for (var i = 0; i < CustomShortcuts.Count; i++)
        {
            if (CustomShortcuts[i].Id == id)
                return i;
        }
        return -1;
    }

    private string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            var number = CustomShortcuts.Count + 1;
            trimmed = $"Shortcut {number}";
            while (CustomShortcuts.Any(c => c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                trimmed = $"Shortcut {++number}";
        }
        return trimmed.Length > 48 ? trimmed[..48] : trimmed;
    }

    private void UpdateShortcutButtons() => CanEditShortcut =
        SelectedProfile is { RecordShortcut: RecordShortcut.Custom, RecordShortcutId: not null and not "" };

    /// <summary>
    /// Creates a new profile. Its name is prefilled from the app that was last
    /// in focus (auto-detected - the user never picks a process), and its
    /// combo starts from the currently selected profile.
    /// </summary>
    public void AddProfile()
    {
        var baseline = SelectedProfile;
        var item = new ProfileItem(
            NextProfileName(),
            baseline?.First ?? ControllerButton.LB,
            baseline?.Second ?? ControllerButton.A);

        Hook(item);
        Profiles.Add(item);
        SelectedProfile = item;
        CanRemoveProfile = Profiles.Count > 1;
        IsRenaming = false;
    }

    private string NextProfileName()
    {
        var (process, title) = _foreground.GetCaptureTarget();
        if (process is not null && !process.Equals("XPadShot", StringComparison.OrdinalIgnoreCase))
            return CleanName(title, process);

        int number = Profiles.Count + 1;
        var name = $"Profile {number}";
        while (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            name = $"Profile {++number}";
        return name;
    }

    public void RemoveSelectedProfile()
    {
        if (Profiles.Count <= 1 || SelectedProfile is null)
            return;

        int index = Profiles.IndexOf(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
        CanRemoveProfile = Profiles.Count > 1;
        Persist();
    }

    public void BeginRename()
    {
        if (SelectedProfile is null)
            return;

        RenameDraft = SelectedProfile.Name;
        IsRenaming = true;
    }

    public void CommitRename()
    {
        if (!IsRenaming)
            return;

        IsRenaming = false;

        var name = RenameDraft.Trim();
        if (name.Length > 0 && SelectedProfile is not null && !name.Equals(SelectedProfile.Name))
            SelectedProfile.Name = name; // hook persists
    }

    public void CancelRename() => IsRenaming = false;

    private static string CleanName(string? title, string process)
    {
        var text = (title ?? "").Trim();
        if (text.Length == 0)
            return process;
        if (text.Length > 48)
            text = text[..45] + "...";
        return text;
    }

    private void Persist() => _settings.Save(new AppSettings
    {
        Profiles = Profiles.Select(p => p.ToProfile()).ToList(),
        CustomShortcuts = CustomShortcuts.ToList(),
        SelectedProfile = SelectedProfile?.Name,
    });

    private void SyncWatcher()
    {
        var source = SelectedProfile ?? Profiles.FirstOrDefault();
        if (source is null)
            return;

        _watcher.First = source.First;
        _watcher.Second = source.Second;
        _watcher.RecordFirst = source.RecordFirst;
        _watcher.RecordSecond = source.RecordSecond;
    }

    private void ApplyConnection(int slot)
    {
        IsConnected = slot >= 0;
        ConnectionText = slot >= 0 ? $"Controller {slot + 1}" : "No controller detected";
        ConnectionHint = slot >= 0
            ? ""
            : "Connect an XInput pad - USB or Bluetooth.";

        if (slot >= 0)
        {
            if (StateWord is "No controller" or "Looking for a controller...")
            {
                StateWord = "Ready";
                StateDetail = "";
            }
        }
        else
        {
            IsComboHeld = false;
            StateWord = "No controller";
            StateDetail = "The combo is parked until a pad connects.";
        }
    }

    private void ApplyComboHeld(bool held)
    {
        IsComboHeld = held;

        if (held)
        {
            if (StateWord == "Ready")
                StateWord = "Combo held";
        }
        else if (StateWord == "Combo held")
        {
            StateWord = "Ready";
        }
    }

    private void TriggerCapture() => _ = CaptureAsync();

    private void TriggerRecording()
    {
        if (SelectedProfile is not { } profile)
            return;

        if (profile.RecordShortcut == RecordShortcut.Custom &&
            FindCustomShortcut(profile.RecordShortcutId) is { } shortcut)
        {
            _ = SendRecordingShortcutAsync(
                () => KeyboardSender.SendCustom(shortcut.Modifiers, shortcut.VirtualKey),
                $"{shortcut.Name} ({ShortcutText.Describe(shortcut.Modifiers, shortcut.VirtualKey)})");
        }
        else
        {
            _ = SendRecordingShortcutAsync(
                KeyboardSender.SendGameBarRecord,
                ShortcutText.GameBarRecordDisplay);
        }
    }

    private async Task SendRecordingShortcutAsync(Action send, string detail)
    {
        if (Interlocked.CompareExchange(ref _recordBusy, 1, 0) != 0)
            return;

        try
        {
            StateWord = "Shortcut sent";
            StateDetail = detail;
            CaptureFlash?.Invoke();

            await Task.Run(send);

            // The shortcuts are toggles (Game Bar and typical recorder hotkeys):
            // alternate the message so start/stop is visible at a glance.
            _recordingActive = !_recordingActive;
            OverlayService.Show(_recordingActive ? "Recording started" : "Recording ended");

            await Task.Delay(1600);

            if (IsComboHeld)
            {
                StateWord = "Combo held";
                StateDetail = "";
            }
            else
            {
                StateWord = "Ready";
                StateDetail = "";
            }
        }
        catch (Exception ex)
        {
            StateWord = "Shortcut failed";
            StateDetail = ex.Message;
        }
        finally
        {
            Volatile.Write(ref _recordBusy, 0);
        }
    }

    private async Task CaptureAsync()
    {
        if (Interlocked.CompareExchange(ref _captureBusy, 1, 0) != 0)
            return;

        try
        {
            StateWord = "Saving...";
            StateDetail = "Capturing the desktop.";

            var target = _foreground.GetCaptureTarget();
            var result = await Task.Run(() => _screenshots.Capture(target.Process, target.Title));

            _captureCount++;
            SessionText = _captureCount == 1
                ? "1 screenshot this session"
                : $"{_captureCount} screenshots this session";

            StateWord = "Saved";
            StateDetail = $"{Path.GetFileName(result.Path)}";
            CaptureFlash?.Invoke();
            _sound.PlayShutter();
            OverlayService.Show("Screenshot taken");

            await Task.Delay(1600);

            if (IsComboHeld)
            {
                StateWord = "Combo held";
                StateDetail = "";
            }
            else
            {
                StateWord = "Ready";
                StateDetail = "";
            }
        }
        catch (Exception ex)
        {
            StateWord = "Capture failed";
            StateDetail = ex.Message;
        }
        finally
        {
            Volatile.Write(ref _captureBusy, 0);
        }
    }

    private static void OnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            action();
        else
            dispatcher.InvokeAsync(action);
    }
}
