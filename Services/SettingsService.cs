using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XPadShot.Models;

namespace XPadShot.Services;

public sealed record AppSettings
{
    public List<GameProfile> Profiles { get; init; } = [];
    public List<CustomShortcut> CustomShortcuts { get; init; } = [];
    public string? SelectedProfile { get; init; }

    // Legacy (pre-1.2) single combo, kept for migration only.
    public ControllerButton? First { get; init; }
    public ControllerButton? Second { get; init; }
}

/// <summary>Loads and saves profiles to %APPDATA%\XPadShot\settings.json.</summary>
public sealed class SettingsService
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XPadShot");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions)
                           ?? new AppSettings();

            // Migrate the pre-profiles format into a single default profile.
            if (settings.Profiles.Count == 0 && (settings.First is not null || settings.Second is not null))
            {
                settings = settings with
                {
                    Profiles =
                    [
                        new GameProfile
                        {
                            Name = "Default",
                            First = settings.First ?? ControllerButton.LB,
                            Second = settings.Second ?? ControllerButton.A,
                        },
                    ],
                    SelectedProfile = "Default",
                };
            }

            return MigrateShortcuts(settings);
        }
        catch
        {
            // corrupted settings: fall back to defaults
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // non-critical: keep running with in-memory settings
        }
    }

    /// <summary>Brings older settings forward: the retired Win+G / Win+Shift+R
    /// presets collapse into the Game Bar record hotkey, and per-profile custom
    /// keys are extracted into the named custom-shortcut library.</summary>
    private static AppSettings MigrateShortcuts(AppSettings settings)
    {
        var customs = settings.CustomShortcuts
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .ToList();

        var profiles = settings.Profiles.Select(profile =>
        {
            var updated = profile;

            if (updated.RecordShortcut is RecordShortcut.GameBarOpen or RecordShortcut.SnippingRecord)
                updated = updated with { RecordShortcut = RecordShortcut.GameBarRecord };

            if (updated.RecordShortcut == RecordShortcut.Custom &&
                string.IsNullOrEmpty(updated.RecordShortcutId) &&
                updated.RecordCustomKey != 0)
            {
                var existing = customs.FirstOrDefault(c =>
                    c.VirtualKey == updated.RecordCustomKey &&
                    c.Modifiers == updated.RecordCustomModifiers);

                if (existing is null)
                {
                    existing = new CustomShortcut
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = NextCustomShortcutName(customs),
                        Modifiers = updated.RecordCustomModifiers,
                        VirtualKey = updated.RecordCustomKey,
                    };
                    customs.Add(existing);
                }

                updated = updated with { RecordShortcutId = existing.Id };
            }

            if (updated.RecordShortcut == RecordShortcut.Custom &&
                (updated.RecordShortcutId is null || customs.All(c => c.Id != updated.RecordShortcutId)))
            {
                updated = updated with { RecordShortcut = RecordShortcut.GameBarRecord, RecordShortcutId = null };
            }

            return updated;
        }).ToList();

        return settings with { Profiles = profiles, CustomShortcuts = customs };
    }

    private static string NextCustomShortcutName(List<CustomShortcut> customs)
    {
        var name = "Custom shortcut";
        var number = 2;
        while (customs.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            name = $"Custom shortcut {number++}";
        return name;
    }
}
