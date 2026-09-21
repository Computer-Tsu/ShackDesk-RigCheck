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
                RigctlCommandBuilder.ExeName = Path.GetFileNameWithoutExtension(path);
                Log.Information("rigctl found via {Via}: {Path}", via, path);
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
        // WSJT-X ships Hamlib in its bin folder under its OWN names:
        // rigctl-wsjtx.exe and rigctld-wsjtx.exe (confirmed on 3.0.2,
        // installed by winget to C:\WSJT\wsjtx). Older builds used plain
        // rigctl.exe, so both are tried. The install folder also comes from
        // the Uninstall key, which covers a non-default install path.
        var wsjtxDirs = new List<string>
        {
            @"C:\WSJT\wsjtx\bin",
            @"C:\Program Files\WSJT-X\bin",
            @"C:\Program Files (x86)\WSJT-X\bin",
        };
        var wsjtxReg = RegistryInstallPath(@"SOFTWARE\WSJT-X", "InstallDir");
        if (wsjtxReg is not null)
            wsjtxDirs.Add(Path.Combine(wsjtxReg, "bin"));
        foreach (var dir in UninstallKeyInstallDirs("wsjtx", "WSJT-X"))
            wsjtxDirs.Add(Path.Combine(dir, "bin"));

        foreach (var d in wsjtxDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return (Path.Combine(d, "rigctl-wsjtx.exe"), "WSJT-X");
            yield return (Path.Combine(d, "rigctl.exe"),       "WSJT-X");
        }

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
        // Either name: a WSJT-X user who added its bin folder to PATH has
        // rigctl-wsjtx, not rigctl.
        foreach (var exe in new[] { "rigctl.exe", "rigctl-wsjtx.exe" })
        {
            var fromPath = FindInPath(exe);
            if (fromPath is not null)
                yield return (fromPath, "PATH");
        }
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

    /// <summary>
    /// Install folders of programs whose Uninstall entry matches any of the
    /// given name fragments: InstallLocation when set, else the folder of
    /// the uninstaller (NSIS installers such as WSJT-X's set only that).
    /// </summary>
    private static IEnumerable<string> UninstallKeyInstallDirs(params string[] nameFragments)
    {
        var found = new List<string>();
        var roots = new (RegistryKey Root, string Sub)[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (root, sub) in roots)
        {
            try
            {
                using var key = root.OpenSubKey(sub);
                if (key is null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var app = key.OpenSubKey(name);
                    var display = app?.GetValue("DisplayName") as string ?? string.Empty;
                    if (!nameFragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)
                                             || display.Contains(f, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var location = app?.GetValue("InstallLocation") as string;
                    if (string.IsNullOrWhiteSpace(location))
                    {
                        var uninstaller = (app?.GetValue("UninstallString") as string ?? string.Empty).Trim('"');
                        location = Path.GetDirectoryName(uninstaller.Split(".exe", StringSplitOptions.None)[0] + ".exe");
                    }
                    if (!string.IsNullOrWhiteSpace(location))
                        found.Add(location.TrimEnd('\\'));
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Uninstall key scan failed under {Root}\\{Sub}", root.Name, sub);
            }
        }
        return found;
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
