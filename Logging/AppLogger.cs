using Serilog;

namespace RigCheck.Logging;

/// <summary>
/// Thin wrapper around Serilog for structured logging.
/// Provides a contextual logger for classes that need one.
/// Mirrors the PortPane AppLogger pattern.
/// </summary>
public class AppLogger
{
    public ILogger ForContext<T>() =>
        Log.ForContext<T>();

    public ILogger ForContext(string propertyName, object value) =>
        Log.ForContext(propertyName, value);

    public void Info(string messageTemplate, params object[] args) =>
        Log.Information(messageTemplate, args);

    public void Debug(string messageTemplate, params object[] args) =>
        Log.Debug(messageTemplate, args);

    public void Warn(string messageTemplate, params object[] args) =>
        Log.Warning(messageTemplate, args);

    public void Error(Exception ex, string messageTemplate, params object[] args) =>
        Log.Error(ex, messageTemplate, args);
}
