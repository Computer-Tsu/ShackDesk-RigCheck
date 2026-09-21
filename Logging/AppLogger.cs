using Serilog;
using Serilog.Events;
using System.IO;

namespace RigCheck.Logging;

/// <summary>
/// Owns the Serilog configuration so the level can change at runtime from
/// Settings and the log files can be released and deleted on request.
/// Instance members are a thin contextual wrapper for classes that take
/// a logger by injection.
/// </summary>
public class AppLogger
{
    // ── Levels offered in Settings ────────────────────────────────────────
    // Stored as these strings; empty means "channel default".
    public const string LevelOff      = "Off";
    public const string LevelErrors   = "Error";
    public const string LevelNormal   = "Information";
    public const string LevelDetailed = "Debug";

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        BrandingInfo.SuiteName, BrandingInfo.AppName, BrandingInfo.LogFolder);

    public static string CurrentLevel { get; private set; } = LevelNormal;

    /// <summary>Test-channel builds log in detail by default; stable logs normally.</summary>
    public static string ChannelDefaultLevel =>
        BuildInfo.IsAlpha || BuildInfo.IsBeta ? LevelDetailed : LevelNormal;

    /// <summary>Resolve a stored setting ("" = channel default) to a concrete level.</summary>
    public static string ResolveLevel(string? stored) =>
        string.IsNullOrEmpty(stored) ? ChannelDefaultLevel : stored;

    // ── Configuration ─────────────────────────────────────────────────────

    /// <summary>Build (or rebuild) the global logger at the given level. Safe to call repeatedly.</summary>
    public static void Configure(string level)
    {
        Log.CloseAndFlush();
        CurrentLevel = level;

        if (level == LevelOff)
        {
            Log.Logger = new LoggerConfiguration().CreateLogger();
            return;
        }

        Directory.CreateDirectory(LogDirectory);

        var minimum = level switch
        {
            LevelErrors   => LogEventLevel.Error,
            LevelDetailed => LogEventLevel.Debug,
            _             => LogEventLevel.Information,
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimum)
            .WriteTo.File(
                path: Path.Combine(LogDirectory, BrandingInfo.LogFileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();
    }

    /// <summary>
    /// Delete every log file. The logger is closed first so the current
    /// day's file is released, then reopened at the same level.
    /// Returns the number of files removed.
    /// </summary>
    public static int DeleteLogs()
    {
        Log.CloseAndFlush();

        var removed = 0;
        if (Directory.Exists(LogDirectory))
        {
            foreach (var file in Directory.GetFiles(LogDirectory, "*.log"))
            {
                try { File.Delete(file); removed++; }
                catch { /* a file held by another process stays; the rest go */ }
            }
        }

        Configure(CurrentLevel);
        Log.Information("Log files deleted by user ({Count} removed)", removed);
        return removed;
    }

    // ── Contextual wrapper ────────────────────────────────────────────────

    public ILogger ForContext<T>() => Log.ForContext<T>();
    public ILogger ForContext(string propertyName, object value) => Log.ForContext(propertyName, value);

    public void Info(string messageTemplate, params object[] args)  => Log.Information(messageTemplate, args);
    public void Debug(string messageTemplate, params object[] args) => Log.Debug(messageTemplate, args);
    public void Warn(string messageTemplate, params object[] args)  => Log.Warning(messageTemplate, args);
    public void Error(Exception ex, string messageTemplate, params object[] args) => Log.Error(ex, messageTemplate, args);
}
