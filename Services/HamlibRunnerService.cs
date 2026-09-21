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
            Arguments              = string.Join(" ", Verbosity.Concat(command.Args).Select(QuoteIfNeeded)),
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            RedirectStandardInput  = true,   // closed right after start: rigctl must never wait for a keyboard
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
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process
                    .WaitForExitAsync(ct)
                    .WaitAsync(TimeSpan.FromMilliseconds(TimeoutMs), ct);
            }
            catch (TimeoutException)
            {
                try { process.Kill(); } catch { /* best effort */ }
                return RigctlResult.Failure(command, RigctlError.Timeout,
                    "rigctl did not respond within the timeout period.");
            }

            var stdout = StripBanner(stdoutBuilder.ToString()).Trim();
            var stderr = stderrBuilder.ToString().Trim();
            var exitCode = process.ExitCode;

            Log.Debug("rigctl exit={Exit} stdout={Out} stderr={Err}",
                exitCode, stdout, stderr);

            // rigctl exits 0 even when the command itself failed (seen with
            // "v" on an IC-7300: "Feature not available", exit 0, plus a
            // trace dump). Treat Hamlib's own error text as failure and
            // keep only the message, never the trace.
            var commandError = HamlibErrorLine(stdout) ?? HamlibErrorLine(stderr);
            if (exitCode == 0 && commandError is null)
                return RigctlResult.Success(command, stdout);

            var error = commandError is not null && commandError.Contains("Feature not available", StringComparison.OrdinalIgnoreCase)
                ? RigctlError.NotSupported
                : ClassifyError(exitCode, stderr, stdout);
            return RigctlResult.Failure(command, error, commandError ?? (stderr.Length > 0 ? stderr : stdout));
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

    // ── Verbosity ─────────────────────────────────────────────────────────
    // At its default verbosity rigctl (Hamlib 4.7.1) says NOTHING when it
    // fails — exit code 2 and empty output — so a port held by WSJT-X, a
    // missing rigctld, and a radio that is switched off all looked the same.
    // "-vv" makes it name the cause on stderr:
    //   serial_open: serial port COM3 is already open
    //   serial_open: serial port COM9 does not exist
    //   network_open: failed to connect to localhost:4532
    // and adds one banner line to stdout on success, which is stripped so
    // the command's own output stays parseable:
    //   Opened rig model 3073, 'IC-7300'

    private static readonly string[] Verbosity = ["-vv"];

    /// <summary>
    /// Hamlib reports a failed command as "get_vfo: error = Feature not
    /// available" or a bare "Feature not available" line, often buried in a
    /// trace dump. Returns that one line, or null when there is none.
    /// </summary>
    private static string? HamlibErrorLine(string output) =>
        output.Split('\n')
              .Select(l => l.Trim())
              .FirstOrDefault(l => l.Contains(": error = ", StringComparison.Ordinal)
                                || l.StartsWith("Feature not available", StringComparison.OrdinalIgnoreCase)
                                || l.StartsWith("Communication timed out", StringComparison.OrdinalIgnoreCase)
                                || l.StartsWith("IO error", StringComparison.OrdinalIgnoreCase));

    private static string StripBanner(string stdout) =>
        string.Join('\n', stdout.Split('\n')
            .Where(l => !l.StartsWith("Opened rig model", StringComparison.Ordinal)));

    // ── Error classification ──────────────────────────────────────────────

    private static RigctlError ClassifyError(int exitCode, string stderr, string stdout)
    {
        var combined = (stderr + stdout).ToLowerInvariant();

        if (combined.Contains("port in use") || combined.Contains("access denied")
            || combined.Contains("sharing violation") || combined.Contains("already open"))
            return RigctlError.PortInUse;

        if (combined.Contains("timeout") || combined.Contains("timed out"))
            return RigctlError.Timeout;

        if (combined.Contains("no such device") || combined.Contains("cannot open")
            || combined.Contains("file not found") || combined.Contains("does not exist"))
            return RigctlError.PortNotFound;

        if (combined.Contains("invalid model") || combined.Contains("unknown rig"))
            return RigctlError.WrongModel;

        if (combined.Contains("connection refused") || combined.Contains("failed to connect")
            || combined.Contains("rigctld"))
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
    /// <summary>The radio's Hamlib backend does not implement this query (get_vfo on many Icoms). Not a fault.</summary>
    NotSupported,
    Cancelled,
    Unknown,
}
