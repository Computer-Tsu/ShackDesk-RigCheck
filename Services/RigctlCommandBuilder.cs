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
    /// <summary>
    /// The name the copyable commands start with. "rigctl" for standalone
    /// Hamlib; WSJT-X ships its copy as "rigctl-wsjtx", and a command the
    /// operator pastes must use the name that actually exists on their PC.
    /// Set by HamlibLocatorService when it finds the exe.
    /// </summary>
    public static string ExeName { get; set; } = "rigctl";

    // ── Connection argument builders ─────────────────────────────────────

    /// <summary>
    /// Build the connection arguments common to every rigctl command
    /// for a direct serial connection. A baud rate of 0 omits -s so
    /// rigctl falls back to the model's default serial speed.
    /// </summary>
    public static RigctlCommand SerialArgs(ConnectionConfig cfg)
    {
        string[] args = ["-m", cfg.ModelId.ToString(), "-r", cfg.ComPort];
        var label = $"-m {cfg.ModelId} -r {cfg.ComPort}";

        if (cfg.BaudRate > 0)
        {
            args  = [..args, "-s", cfg.BaudRate.ToString()];
            label = $"{label} -s {cfg.BaudRate}";
        }

        return new(Args: args, ConnectionLabel: label);
    }

    /// <summary>
    /// Build connection args for a rigctld network connection.
    /// </summary>
    public static RigctlCommand NetworkArgs(ConnectionConfig cfg) =>
        new(
            Args: ["-m", "2",   // model 2 = rigctld network backend
                   "-r", $"{cfg.RigctldHost}:{cfg.RigctldPort}"],
            ConnectionLabel: $"-m 2 -r {cfg.RigctldHost}:{cfg.RigctldPort}");

    // ── Test commands ────────────────────────────────────────────────────

    /// <summary>
    /// Test 1: Open connection. Asks for the frequency: rigctl with no
    /// subcommand does not "just connect", it enters interactive mode and
    /// waits on stdin forever — which read as a radio timeout on a working
    /// IC-7300. Hamlib's open already exchanges with the rig; the reply to
    /// "f" proves the round trip.
    /// </summary>
    public static RigctlCommand TestConnection(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args           = [..conn.Args, "f"],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} f",
        };
    }

    /// <summary>Test 2: Get frequency — 'f' subcommand.</summary>
    public static RigctlCommand GetFrequency(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "f"],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} f"
        };
    }

    /// <summary>Test 3: Get mode — 'm' subcommand.</summary>
    public static RigctlCommand GetMode(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "m"],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} m"
        };
    }

    /// <summary>Test 4: Get PTT state — 't' subcommand.</summary>
    public static RigctlCommand GetPtt(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "t"],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} t"
        };
    }

    /// <summary>Test 5: Get signal meter (S-meter) — 'l STRENGTH' subcommand.</summary>
    public static RigctlCommand GetSmeter(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "l", "STRENGTH"],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} l STRENGTH"
        };
    }

    /// <summary>Test 6: Get active VFO — 'v' subcommand.</summary>
    public static RigctlCommand GetVfo(ConnectionConfig cfg)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "v"],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} v"
        };
    }

    /// <summary>Test 7a: Set frequency — 'F {hz}' subcommand.</summary>
    public static RigctlCommand SetFrequency(ConnectionConfig cfg, long frequencyHz)
    {
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, "F", frequencyHz.ToString()],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} F {frequencyHz}"
        };
    }

    // ── Raw console ──────────────────────────────────────────────────────

    /// <summary>
    /// Build a command from a raw subcommand string the user typed.
    /// Splits the input into tokens and appends to connection args.
    /// </summary>
    public static RigctlCommand RawCommand(ConnectionConfig cfg, string rawInput)
    {
        var tokens = StripConnectionPrefix(rawInput.Trim().Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var conn = ConnArgs(cfg);
        return conn with
        {
            Args        = [..conn.Args, ..tokens],
            DisplayCommand = $"{ExeName} {conn.ConnectionLabel} {string.Join(' ', tokens)}"
        };
    }

    // Operators paste the whole line the results panel shows them —
    // "rigctl-wsjtx -m 3073 -r COM3 -s 115200 f" — into a console that only
    // wants "f". Drop the exe name and the connection options so the panel's
    // settings apply once, not twice.
    private static readonly HashSet<string> ConnectionOptions =
        ["-m", "-r", "-s", "-t", "-C", "-p", "-P", "-c", "--model", "--rig-file", "--serial-speed"];

    private static string[] StripConnectionPrefix(string[] tokens)
    {
        if (tokens.Length == 0 || !tokens[0].StartsWith("rigctl", StringComparison.OrdinalIgnoreCase))
            return tokens;

        var rest = new List<string>();
        for (int i = 1; i < tokens.Length; i++)
        {
            if (ConnectionOptions.Contains(tokens[i]) && i + 1 < tokens.Length) { i++; continue; }
            if (tokens[i].StartsWith("-v", StringComparison.Ordinal)) continue;
            rest.Add(tokens[i]);
        }
        return rest.ToArray();
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
