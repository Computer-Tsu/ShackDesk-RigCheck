using RigCheck.Models;

namespace RigCheck.Services;

/// <summary>
/// Builds rigctl.exe command arguments for every RigCheck operation.
///
/// DESIGN INTENT — "Show the command" pattern (Exchange MMC / PowerShell model):
/// Every method returns both the argument list (for execution) and a
/// human-readable command string the user can copy and run themselves
/// in their own CMD window. This demystifies rig control and lets
/// operators learn, save favorites, and graduate to the command line.
///
/// Example output shown in UI:
///   rigctl -m 3073 -r COM4 -s 19200 f
///   ^---- users can copy this and run it themselves
/// </summary>
public class RigctlCommandBuilder
{
    // ── Connection argument builders ─────────────────────────────────────

    /// <summary>
    /// Build the connection arguments common to every rigctl command
    /// for a direct serial connection.
    /// </summary>
    public static RigctlCommand SerialArgs(ConnectionConfig cfg) =>
        new(
            Args: ["-m", cfg.ModelId.ToString(),
                   "-r", cfg.ComPort,
                   "-s", cfg.BaudRate.ToString()],
            ConnectionLabel: $"-m {cfg.ModelId} -r {cfg.ComPort} -s {cfg.BaudRate}");

    /// <summary>
    /// Build connection args for a rigctld network connection.
    /// </summary>
    public static RigctlCommand NetworkArgs(ConnectionConfig cfg) =>
        new(
            Args: ["-m", "2",   // model 2 = rigctld network backend
                   "-r", $"{cfg.RigctldHost}:{cfg.RigctldPort}"],
            ConnectionLabel: $"-m 2 -r {cfg.RigctldHost}:{cfg.RigctldPort}");

    // ── Test commands ────────────────────────────────────────────────────

    /// <summary>Test 1: Open connection — just connect and disconnect (no subcommand).</summary>
    public static RigctlCommand TestConnection(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with { DisplayCommand = $"rigctl {conn.ConnectionLabel}" };
    }

    /// <summary>Test 2: Get frequency — 'f' subcommand.</summary>
    public static RigctlCommand GetFrequency(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "f"],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} f"
        };
    }

    /// <summary>Test 3: Get mode — 'm' subcommand.</summary>
    public static RigctlCommand GetMode(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "m"],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} m"
        };
    }

    /// <summary>Test 4: Get PTT state — 't' subcommand.</summary>
    public static RigctlCommand GetPtt(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "t"],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} t"
        };
    }

    /// <summary>Test 5: Get signal meter (S-meter) — 'l STRENGTH' subcommand.</summary>
    public static RigctlCommand GetSmeter(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "l", "STRENGTH"],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} l STRENGTH"
        };
    }

    /// <summary>Test 6: Get active VFO — 'v' subcommand.</summary>
    public static RigctlCommand GetVfo(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "v"],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} v"
        };
    }

    /// <summary>Test 7a: Set frequency — 'F {hz}' subcommand.</summary>
    public static RigctlCommand SetFrequency(ConnectionConfig cfg, long frequencyHz)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "F", frequencyHz.ToString()],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} F {frequencyHz}"
        };
    }

    // ── Raw console ──────────────────────────────────────────────────────

    /// <summary>
    /// Build a command from a raw subcommand string the user typed.
    /// Splits the input into tokens and appends to connection args.
    /// </summary>
    public static RigctlCommand RawCommand(ConnectionConfig cfg, string rawInput)
    {
        var tokens = rawInput.Trim().Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, ..tokens],
            DisplayCommand = $"rigctl {conn.ConnectionLabel} {rawInput.Trim()}"
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static RigctlCommand ConnArgs(ConnectionConfig cfg) =>
        cfg.UseRigctld ? NetworkArgs(cfg) : SerialArgs(cfg);
}

/// <summary>
/// Immutable descriptor for a rigctl invocation.
/// Args    → passed to Process.Start
/// DisplayCommand → shown in UI so the user can copy and run it themselves
/// </summary>
public record RigctlCommand(
    string[]? Args           = null,
    string?   ConnectionLabel = null,
    string?   DisplayCommand  = null)
{
    public string[] Args { get; init; } = Args ?? [];

    /// <summary>
    /// Full command the user can paste into CMD and run themselves.
    /// Includes the rigctl.exe name (not the full path, for readability).
    /// </summary>
    public string DisplayCommand { get; init; } = DisplayCommand ?? string.Empty;
}
