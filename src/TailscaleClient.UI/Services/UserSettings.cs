using System.IO;
using System.Text.Json;

namespace TailscaleClient.UI.Services;

/// <summary>
/// Tiny JSON-backed per-user settings store. Persists to
/// <c>%APPDATA%\TailscaleClient\settings.json</c>.
/// </summary>
public sealed class UserSettings
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private SettingsData _data;

    public UserSettings()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TailscaleClient");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        _data = Load();
    }

    public event Action? TaildropAutoSaveFolderChanged;

    public string? TaildropAutoSaveFolder
    {
        get => _data.TaildropAutoSaveFolder;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (_data.TaildropAutoSaveFolder == normalized) return;
            _data.TaildropAutoSaveFolder = normalized;
            Save();
            TaildropAutoSaveFolderChanged?.Invoke();
        }
    }

    private SettingsData Load()
    {
        try
        {
            if (!File.Exists(_path)) return new SettingsData();
            return JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path), JsonOpts)
                   ?? new SettingsData();
        }
        catch { return new SettingsData(); }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOpts)); }
        catch { /* non-fatal */ }
    }

    private sealed class SettingsData
    {
        public string? TaildropAutoSaveFolder { get; set; }
    }
}
