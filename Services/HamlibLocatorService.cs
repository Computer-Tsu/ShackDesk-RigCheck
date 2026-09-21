using Microsoft.Win32;
using Serilog;
using System.IO;

namespace RigCheck.Services;

/// <summary>
/// Locates rigctl.exe on the user's machine.
/// Checks known install paths for WSJT-X, Fldigi, and standalone
/// Hamlib for Windows, then falls back to PATH.
///
/// Offline-first: never throws. Returns null if not found.
/// </summary>
public class HamlibLocatorService
{
    private string? _cachedPath;
    private bool    _searched;

    /// <summary>
    /// Full path to rigctl.exe, or null if not found.
    /// Result is cached after first call.
    /// </summary>
    public string? RigctlPath
    {
        get
        {
            if (!_searched) Find();
            return _cachedPath;
        }
    }

    /// <summary>True if rigctl.exe was found anywhere on the system.</summary>
    public bool IsAvailable => RigctlPath is not null;

    /// <summary>Human-readable description of where rigctl.exe was found.</summary>
    public string? FoundVia { get; private set; }

    // ── Search ───────────────────────────────────────────────────────────

    public void Find()
    {
        _searched = true;
        _cachedPath = null;
        FoundVia = null;

        foreach (var (path, via) in CandidatePaths())
        {
            if (File.Exists(path))
            {
                _cachedPath = path;
                FoundVia = via;
                Log.Information("rigctl.exe found via {Via}: {Path}", via, path);
                return;
            }
        }

        Log.Warning("rigctl.exe not found. Hamlib may not be installed.");
    }

    /// <summary>
    /// Every rigctl.exe that actually exists, in priority order, first one
    /// being the copy RigCheck uses. Several is normal — WSJT-X and Fldigi
    /// each bundle their own — and the environment scan lists them all.
    /// </summary>
    public IReadOnlyList<(string Path, string Via)> FindAll() =>
        CandidatePaths()
            .Where(c => File.Exists(c.Path))
            .DistinctBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Returns all candidate (path, source-label) pairs in priority order.
    /// </summary>
    public IEnumerable<(string Path, string Via)> CandidatePaths()
    {
        // ── 1. WSJT-X bundled Hamlib ─────────────────────────────────────
        // WSJT-X ships rigctl.exe in its own bin directory.
        var wsjtxDirs = new[]
        {
            @"C:\WSJT\wsjtx\bin",
            @"C:\Program Files\WSJT-X\bin",
            @"C:\Program Files (x86)\WSJT-X\bin",
        };
        // Also check registry for WSJT-X install location
        var wsjtxReg = RegistryInstallPath(@"SOFTWARE\WSJT-X", "InstallDir");
        if (wsjtxReg is not null)
            wsjtxDirs = [..wsjtxDirs, Path.Combine(wsjtxReg, "bin")];

        foreach (var d in wsjtxDirs)
            yield return (Path.Combine(d, "rigctl.exe"), "WSJT-X");

        // ── 2. Fldigi bundled Hamlib ──────────────────────────────────────
        var fldigiDirs = new[]
        {
            @"C:\Program Files\Fldigi\",
            @"C:\Program Files (x86)\Fldigi\",
        };
        var fldigiReg = RegistryInstallPath(@"SOFTWARE\Fldigi", "InstallDir");
        if (fldigiReg is not null)
            fldigiDirs = [..fldigiDirs, fldigiReg];

        foreach (var d in fldigiDirs)
            yield return (Path.Combine(d, "rigctl.exe"), "Fldigi");

        // ── 3. Standalone Hamlib for Windows ──────────────────────────────
        // https://github.com/Hamlib/Hamlib/releases — installs to Hamlib4
        var hamlibDirs = new[]
        {
            @"C:\Hamlib4\bin",
            @"C:\Hamlib\bin",
            @"C:\Program Files\Hamlib\bin",
            @"C:\Program Files (x86)\Hamlib\bin",
        };
        foreach (var d in hamlibDirs)
            yield return (Path.Combine(d, "rigctl.exe"), "Hamlib standalone");

        // ── 4. PATH ───────────────────────────────────────────────────────
        var fromPath = FindInPath("rigctl.exe");
        if (fromPath is not null)
            yield return (fromPath, "PATH");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string? FindInPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static string? RegistryInstallPath(string subKey, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey)
                         ?? Registry.CurrentUser.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Registry lookup failed for {SubKey}\\{Value}", subKey, valueName);
            return null;
        }
    }

    // ── PATH management helpers ───────────────────────────────────────────
    // Display only. RigCheck never modifies PATH; the user is shown the
    // command to run themselves in an elevated prompt.

    public static string PathAddCommand(string hamlibBinDir) =>
        $"setx /M PATH \"%PATH%;{hamlibBinDir}\"";

    public static string PathAddPowerShell(string hamlibBinDir) =>
        $"[Environment]::SetEnvironmentVariable('Path', $env:Path + ';{hamlibBinDir}', 'Machine')";
}
