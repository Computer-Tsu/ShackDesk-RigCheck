using RigCheck.Models;
using Serilog;
using System.Diagnostics;
using System.Text;

namespace RigCheck.Services;

/// <summary>
/// Executes rigctl.exe as a subprocess and returns the output.
///
/// Each test call is a fresh rigctl invocation (rigctl is stateless
/// by default — it opens, runs the command, and exits).
///
/// The DisplayCommand on every result is the exact command the user
/// can copy and paste into their own CMD window.
/// </summary>
public class HamlibRunnerService
{
    private readonly HamlibLocatorService _locator;

    // Timeout for a single rigctl invocation
    private const int TimeoutMs = 5_000;

    public HamlibRunnerService(HamlibLocatorService locator)
    {
        _locator = locator;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Run a rigctl command and return the raw output and exit code.
    /// Never throws — errors are captured in RigctlResult.
    /// </summary>
    public async Task<RigctlResult> RunAsync(RigctlCommand command,
                                             CancellationToken ct = default)
    {
        if (!_locator.IsAvailable)
        {
            return RigctlResult.Failure(
                command,
                RigctlError.HamlibNotFound,
                "rigctl.exe not found. Hamlib may not be installed.");
        }

        var rigctlPath = _locator.RigctlPath!;

        var startInfo = new ProcessStartInfo
        {
            FileName               = rigctlPath,
            Arguments              = string.Join(" ", command.Args.Select(QuoteIfNeeded)),
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        Log.Debug("rigctl: {Command}", command.DisplayCommand);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) stdoutBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) stderrBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await process
                .WaitForExitAsync(ct)
                .WaitAsync(TimeSpan.FromMilliseconds(TimeoutMs), ct);

            if (!completed)
            {
                try { process.Kill(); } catch { /* best effort */ }
                return RigctlResult.Failure(command, RigctlError.Timeout,
                    "rigctl did not respond within the timeout period.");
            }

            var stdout = stdoutBuilder.ToString().Trim();
            var stderr = stderrBuilder.ToString().Trim();
            var exitCode = process.ExitCode;

            Log.Debug("rigctl exit={Exit} stdout={Out} stderr={Err}",
                exitCode, stdout, stderr);

            if (exitCode == 0)
                return RigctlResult.Success(command, stdout);

            // Exit code non-zero — classify the error
            var error = ClassifyError(exitCode, stderr, stdout);
            return RigctlResult.Failure(command, error, stderr.Length > 0 ? stderr : stdout);
        }
        catch (OperationCanceledException)
        {
            return RigctlResult.Failure(command, RigctlError.Cancelled, "Operation cancelled.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unexpected error running rigctl");
            return RigctlResult.Failure(command, RigctlError.Unknown, ex.Message);
        }
    }

    // ── Error classification ──────────────────────────────────────────────

    private static RigctlError ClassifyError(int exitCode, string stderr, string stdout)
    {
        var combined = (stderr + stdout).ToLowerInvariant();

        if (combined.Contains("port in use") || combined.Contains("access denied")
            || combined.Contains("sharing violation"))
            return RigctlError.PortInUse;

        if (combined.Contains("timeout") || combined.Contains("timed out"))
            return RigctlError.Timeout;

        if (combined.Contains("no such device") || combined.Contains("cannot open")
            || combined.Contains("file not found"))
            return RigctlError.PortNotFound;

        if (combined.Contains("invalid model") || combined.Contains("unknown rig"))
            return RigctlError.WrongModel;

        if (combined.Contains("connection refused") || combined.Contains("rigctld"))
            return RigctlError.RigctldNotRunning;

        if (combined.Contains("no response") || combined.Contains("io_err"))
            return RigctlError.NoResponse;

        return RigctlError.Unknown;
    }

    private static string QuoteIfNeeded(string arg) =>
        arg.Contains(' ') ? $"\"{arg}\"" : arg;
}

// ── Result types ──────────────────────────────────────────────────────────

public record RigctlResult(
    RigctlCommand Command,
    bool          IsSuccess,
    string        RawOutput,
    RigctlError   Error,
    string        ErrorMessage)
{
    public static RigctlResult Success(RigctlCommand cmd, string output) =>
        new(cmd, true, output, RigctlError.None, string.Empty);

    public static RigctlResult Failure(RigctlCommand cmd, RigctlError error, string message) =>
        new(cmd, false, string.Empty, error, message);

    /// <summary>The command the user can copy and run in their own CMD window.</summary>
    public string DisplayCommand => Command.DisplayCommand;
}

public enum RigctlError
{
    None,
    HamlibNotFound,
    PortInUse,
    PortNotFound,
    Timeout,
    NoResponse,
    WrongModel,
    RigctldNotRunning,
    Cancelled,
    Unknown,
}
