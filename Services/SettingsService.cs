using Newtonsoft.Json;
using Serilog;
using System.IO;

namespace RigCheck.Services;

// ── Settings model ────────────────────────────────────────────────────────

/// <summary>
/// All persisted user preferences for RigCheck.
/// Serialized to JSON in %LOCALAPPDATA%\ShackDesk\RigCheck\.
/// </summary>
public class RigCheckSettings
{
    // Connection
    public string  ComPort         { get; set; } = string.Empty;
    public int     BaudRate        { get; set; } = BrandingInfo.DefaultBaudRate;
    public string  DataBits        { get; set; } = BrandingInfo.DefaultDataBits;
    public string  Parity          { get; set; } = BrandingInfo.DefaultParity;
    public string  StopBits        { get; set; } = BrandingInfo.DefaultStopBits;
    public string  FlowControl     { get; set; } = BrandingInfo.DefaultFlowCtrl;
    public string  PttMethod       { get; set; } = BrandingInfo.DefaultPttMethod;
    public int     RadioModelId    { get; set; } = 0;   // Hamlib rig ID; 0 = none chosen
    public string  RadioModelName  { get; set; } = string.Empty;

    // Network / rigctld
    public bool   UseRigctld      { get; set; } = false;
    public string RigctldHost     { get; set; } = BrandingInfo.DefaultRigctldHost;
    public int    RigctldPort     { get; set; } = BrandingInfo.DefaultRigctldPort;

    // Test options
    public bool   RunSetFreqTest  { get; set; } = false;   // optional — user must confirm
    public bool   IncludeSmeter   { get; set; } = true;

    // Window state
    public double WindowWidth     { get; set; } = BrandingInfo.DefaultWindowWidth;
    public double WindowHeight    { get; set; } = BrandingInfo.DefaultWindowHeight;
    public double WindowLeft      { get; set; } = double.NaN;
    public double WindowTop       { get; set; } = double.NaN;
    public bool   AlwaysOnTop     { get; set; } = false;
    public double ScaleFactor     { get; set; } = 1.0;

    // Notifications when a run or scan finishes. Flash only matters when the
    // window is in the background; sound is off by default because a shack
    // PC often shares speakers with the radio audio.
    public bool   NotifyFlash     { get; set; } = true;
    public bool   NotifySound     { get; set; } = false;

    // UI state
    public bool   RawConsoleOpen  { get; set; } = false;
    public string LastPresetName  { get; set; } = string.Empty;

    // Command history for raw console (most recent first)
    public List<string> CommandHistory { get; set; } = [];

    // Telemetry. InstallId is a random GUID created once per install; it is the
    // only identifier ever sent and lets a support request be matched to reports.
    public string InstallId         { get; set; } = Guid.NewGuid().ToString("D");
    public bool   TelemetryEnabled  { get; set; } = false;
    public bool   TelemetryPrompted { get; set; } = false;

    // Logging level; empty means the channel default (see AppLogger).
    public string LogLevel          { get; set; } = string.Empty;

    // UI language as a culture name ("de", "ja"); empty = follow Windows.
    // Takes effect on the next start.
    public string Language          { get; set; } = string.Empty;

    // Update check: one request to the GitHub Releases API, at most once a
    // day, at startup. On by default for alpha and beta (their users want the
    // next build); stable users switch it on in Settings. Nothing is ever
    // downloaded — a newer release is named and its page can be opened.
    // Null means "not chosen yet": the channel default applies.
    public bool?    UpdateCheckEnabled { get; set; } = null;
    public DateTime UpdateLastChecked  { get; set; } = DateTime.MinValue;
    public string   UpdateLastSeenTag  { get; set; } = string.Empty;
}

// ── Settings service ──────────────────────────────────────────────────────

/// <summary>
/// Loads and saves RigCheckSettings to the user's AppData folder.
/// Offline-first: never throws on load failure, returns defaults instead.
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private RigCheckSettings _current;

    public RigCheckSettings Current => _current;

    public SettingsService()
    {
        var dir = SettingsDirectory;
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, BrandingInfo.SettingsFileName);
        _current = Load();
    }

    public static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            BrandingInfo.SuiteName,
            BrandingInfo.AppName);

    /// <summary>
    /// Load settings from disk. Returns defaults if file missing or corrupt.
    /// </summary>
    public RigCheckSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                Log.Debug("Settings file not found, using defaults: {Path}", _settingsPath);
                return new RigCheckSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonConvert.DeserializeObject<RigCheckSettings>(json);

            if (settings is null)
            {
                Log.Warning("Settings deserialized as null, using defaults");
                return new RigCheckSettings();
            }

            Log.Debug("Settings loaded from {Path}", _settingsPath);
            _current = settings;
            return settings;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings, using defaults");
            return new RigCheckSettings();
        }
    }

    /// <summary>
    /// Save current settings to disk. Silently logs on failure.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_current, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
            Log.Debug("Settings saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save settings to {Path}", _settingsPath);
        }
    }

    /// <summary>
    /// Update a property and immediately persist.
    /// </summary>
    public void Update(Action<RigCheckSettings> mutate)
    {
        mutate(_current);
        Save();
    }
}
