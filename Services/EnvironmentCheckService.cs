using Microsoft.Win32;
using RigCheck.Localization;
using RigCheck.Models;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace RigCheck.Services;

/// <summary>
/// Pre-flight checks on the PC itself — no radio needed. Answers the
/// questions a helper would ask first: is Hamlib installed and which copy,
/// does rigctl run, are the USB-serial drivers healthy, is rigctld already
/// running, has Windows Firewall blocked it.
///
/// Everything here is read-only. RigCheck never modifies PATH, firewall
/// rules, startup entries, or drivers; where a fix is needed it shows the
/// command or the setting to change and lets the operator do it.
///
/// Runs only when the operator clicks Scan. Enumerating processes and
/// listening ports unprompted is a pattern security software watches for,
/// so none of this happens at startup.
/// </summary>
public class EnvironmentCheckService
{
    private readonly HamlibLocatorService _locator;
    private readonly HamlibRunnerService  _runner;
    private readonly ComPortService       _ports;

    public EnvironmentCheckService(HamlibLocatorService locator,
                                   HamlibRunnerService  runner,
                                   ComPortService       ports)
    {
        _locator = locator;
        _runner  = runner;
        _ports   = ports;
    }

    /// <summary>The checks, in run order — used for the pending placeholders.</summary>
    public static readonly IReadOnlyList<TestId> Checks =
    [
        TestId.EnvWindows,     TestId.EnvHamlib,   TestId.EnvRigctlVersion, TestId.EnvRigctlPath,
        TestId.EnvRadioModel,  TestId.EnvSerialDrivers, TestId.EnvRigctld, TestId.EnvAutostart,
        TestId.EnvFirewall,    TestId.EnvRadioApps,
    ];

    public async Task<TestSuiteResult> RunAllAsync(
        ConnectionConfig cfg,
        IProgress<TestResult>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<TestResult>();

        // Each check is isolated: one throwing must not hide the others.
        async Task Run(TestId id, Func<Task<TestResult>> check)
        {
            if (ct.IsCancellationRequested) return;
            TestResult result;
            try
            {
                result = await check();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Environment check {Check} failed", id);
                result = TestResult.Warning(id, Strings.Format("Env_CheckFailed", ex.Message), string.Empty);
            }
            results.Add(result);
            progress?.Report(result);
        }

        await Run(TestId.EnvWindows,       () => Task.FromResult(CheckWindows()));
        await Run(TestId.EnvHamlib,        () => Task.FromResult(CheckHamlib()));
        await Run(TestId.EnvRigctlVersion, () => CheckRigctlVersionAsync(ct));
        await Run(TestId.EnvRigctlPath,    () => Task.FromResult(CheckRigctlOnPath()));
        await Run(TestId.EnvRadioModel,    () => Task.FromResult(CheckRadioModel(cfg)));
        await Run(TestId.EnvSerialDrivers, () => Task.Run(CheckSerialDrivers, ct));
        await Run(TestId.EnvRigctld,       () => Task.Run(() => CheckRigctld(cfg), ct));
        await Run(TestId.EnvAutostart,     () => Task.FromResult(CheckAutostart()));
        await Run(TestId.EnvFirewall,      () => Task.FromResult(CheckFirewall()));
        await Run(TestId.EnvRadioApps,     () => Task.FromResult(CheckRadioApps()));

        return new TestSuiteResult(results, cfg);
    }

    // ── Windows ───────────────────────────────────────────────────────────

    private static TestResult CheckWindows()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var product = key?.GetValue("ProductName")    as string ?? "Windows";
        var release = key?.GetValue("DisplayVersion") as string ?? string.Empty;
        var build   = Environment.OSVersion.Version.Build;

        // The registry still says "Windows 10" on Windows 11; the build number tells the truth.
        if (build >= 22000) product = product.Replace("Windows 10", "Windows 11");

        var arch = RuntimeInformation.OSArchitecture.ToString();
        return TestResult.Pass(TestId.EnvWindows,
            Strings.Format("Env_Windows", product, release, build, arch), string.Empty);
    }

    // ── Hamlib ────────────────────────────────────────────────────────────

    private TestResult CheckHamlib()
    {
        var copies = _locator.FindAll();
        if (copies.Count == 0)
        {
            return TestResult.Fail(TestId.EnvHamlib, Strings.Get("Env_HamlibNone"), string.Empty,
                Diag("Env_HamlibNone", 2, fixCommand: BrandingInfo.WsjtxWingetCommand, learnMore: BrandingInfo.HamlibDownloadUrl));
        }

        var (path, via) = copies[0];
        var message = copies.Count == 1
            ? Strings.Format("Env_HamlibOne", via, path)
            : Strings.Format("Env_HamlibMany", copies.Count, via, path,
                             string.Join("; ", copies.Skip(1).Select(c => $"{c.Via}: {c.Path}")));

        return TestResult.Pass(TestId.EnvHamlib, message, string.Empty);
    }

    private async Task<TestResult> CheckRigctlVersionAsync(CancellationToken ct)
    {
        if (!_locator.IsAvailable)
            return TestResult.Skipped(TestId.EnvRigctlVersion, Strings.Get("Env_SkippedNoHamlib"));

        var cmd = new RigctlCommand(["--version"], DisplayCommand: "rigctl --version");
        var result = await _runner.RunAsync(cmd, ct);

        if (!result.IsSuccess)
        {
            return TestResult.Fail(TestId.EnvRigctlVersion,
                Strings.Format("Env_RigctlVersionFail", result.ErrorMessage), cmd.DisplayCommand,
                Diag("Env_RigctlVersionFail", 2));
        }

        // First line is "rigctl Hamlib 4.6.2 ..." — enough on its own.
        var firstLine = result.RawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault()?.Trim() ?? result.RawOutput.Trim();
        return TestResult.Pass(TestId.EnvRigctlVersion, firstLine, cmd.DisplayCommand);
    }

    // The commands RigCheck shows say "rigctl ..." — they only work in the
    // operator's own command window if Hamlib's bin folder is on PATH.
    private TestResult CheckRigctlOnPath()
    {
        if (!_locator.IsAvailable)
            return TestResult.Skipped(TestId.EnvRigctlPath, Strings.Get("Env_SkippedNoHamlib"));

        var onPath = _locator.FindAll().FirstOrDefault(c => c.Via == "PATH");
        if (onPath.Path is not null)
            return TestResult.Pass(TestId.EnvRigctlPath, Strings.Format("Env_RigctlPathYes", onPath.Path), string.Empty);

        var binDir = Path.GetDirectoryName(_locator.RigctlPath!) ?? string.Empty;
        return TestResult.Warning(TestId.EnvRigctlPath, Strings.Get("Env_RigctlPathNo"), string.Empty) with
        {
            Diagnosis = Diag("Env_RigctlPathNo", 2, fixCommand: HamlibLocatorService.PathAddCommand(binDir)),
        };
    }

    // ── Radio model ───────────────────────────────────────────────────────
    // Model 1 is Hamlib's dummy rig: every command succeeds without touching
    // hardware. The exported log must make it unmistakable which model was
    // in use, so this check always runs.

    private static TestResult CheckRadioModel(ConnectionConfig cfg) => cfg.ModelId switch
    {
        0 => TestResult.Warning(TestId.EnvRadioModel, Strings.Get("Env_RadioModelNone"), string.Empty),
        1 => TestResult.Warning(TestId.EnvRadioModel, Strings.Get("Env_RadioModelDummy"), string.Empty) with
             {
                 Diagnosis = Diag("Env_RadioModelDummy", 1, learnMore: BrandingInfo.HelpUrl + "-dummy-rig"),
             },
        _ => TestResult.Pass(TestId.EnvRadioModel,
                 Strings.Format("Env_RadioModel", cfg.RadioModelName, cfg.ModelId), string.Empty),
    };

    // ── Serial drivers ────────────────────────────────────────────────────
    // A cable whose driver failed does not get a COM number, so it never
    // shows in the port list. Ask Plug and Play for every device with a
    // problem code and keep the ones that look like serial adapters.

    private TestResult CheckSerialDrivers()
    {
        var ports = _ports.GetAvailablePorts();
        var problems = new List<string>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
        foreach (ManagementObject dev in searcher.Get())
        {
            var name  = dev["Name"]?.ToString()     ?? string.Empty;
            var cls   = dev["PNPClass"]?.ToString() ?? string.Empty;
            var code  = Convert.ToInt32(dev["ConfigManagerErrorCode"] ?? 0);
            if (cls is "Ports" || LooksLikeSerialAdapter(name))
                problems.Add(Strings.Format("Env_SerialProblem", name, code));
        }

        if (problems.Count > 0)
        {
            return TestResult.Warning(TestId.EnvSerialDrivers, string.Join("; ", problems), string.Empty) with
            {
                Diagnosis = Diag("Env_SerialProblem", 3),
            };
        }

        if (ports.Count == 0)
        {
            return TestResult.Warning(TestId.EnvSerialDrivers, Strings.Get("Env_SerialNone"), string.Empty) with
            {
                Diagnosis = Diag("Env_SerialNone", 2),
            };
        }

        var list = string.Join(", ", ports.Select(p => p.PortName));
        return TestResult.Pass(TestId.EnvSerialDrivers, Strings.Format("Env_SerialOk", ports.Count, list), string.Empty);
    }

    private static bool LooksLikeSerialAdapter(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("serial") || n.Contains("uart") || n.Contains("cp210") ||
               n.Contains("ftdi")   || n.Contains("ft232") || n.Contains("prolific") ||
               n.Contains("ch340")  || n.Contains("ch341") || n.Contains("usb-to-serial") ||
               n.Contains("unknown device");
    }

    // ── rigctld ───────────────────────────────────────────────────────────

    private static TestResult CheckRigctld(ConnectionConfig cfg)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        var port      = cfg.UseRigctld ? cfg.RigctldPort : BrandingInfo.DefaultRigctldPort;
        var listening = listeners.Any(l => l.Port == port);
        var flrig     = listeners.Any(l => l.Port == 12345);

        var extra = flrig ? "  " + Strings.Format("Env_FlrigListening", 12345) : string.Empty;

        if (listening)
        {
            // Name the owner if it is obviously rigctld; anything else stays "unknown".
            var rigctld = Process.GetProcessesByName("rigctld");
            var owner   = rigctld.Length > 0 ? "rigctld.exe" : Strings.Get("Env_ProcessUnknown");
            foreach (var p in rigctld) p.Dispose();
            return TestResult.Pass(TestId.EnvRigctld,
                Strings.Format("Env_RigctldListening", port, owner) + extra, string.Empty);
        }

        if (cfg.UseRigctld)
        {
            return TestResult.Warning(TestId.EnvRigctld, Strings.Format("Env_RigctldNeeded", port) + extra, string.Empty) with
            {
                Diagnosis = Diag("Env_RigctldNeeded", 2),
            };
        }

        return TestResult.Pass(TestId.EnvRigctld, Strings.Format("Env_RigctldNone", port) + extra, string.Empty);
    }

    // Startup folders and Run keys only. Scheduled tasks would need
    // schtasks output parsing; not worth it for something this rare.
    private static TestResult CheckAutostart()
    {
        var found = new List<string>();

        foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
        {
            var dir = Environment.GetFolderPath(folder);
            if (!Directory.Exists(dir)) continue;
            found.AddRange(Directory.EnumerateFiles(dir)
                .Where(f => Path.GetFileName(f).Contains("rigctld", StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var run = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (run is null) continue;
            foreach (var name in run.GetValueNames())
            {
                var value = run.GetValue(name)?.ToString() ?? string.Empty;
                if (value.Contains("rigctld", StringComparison.OrdinalIgnoreCase))
                    found.Add($"{root.Name}\\...\\Run\\{name} = {value}");
            }
        }

        return found.Count == 0
            ? TestResult.Pass(TestId.EnvAutostart, Strings.Get("Env_AutostartNone"), string.Empty)
            : TestResult.Pass(TestId.EnvAutostart, Strings.Format("Env_AutostartFound", string.Join("; ", found)), string.Empty);
    }

    // Firewall rules live in the registry as "v2.x|Action=Block|Active=TRUE|
    // Dir=In|App=C:\...\rigctld.exe|Name=...|". Reading them needs no
    // elevation and no netsh. A Block rule is the classic result of
    // dismissing the firewall prompt the first time rigctld listened.
    private static TestResult CheckFirewall()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules");
        if (key is null)
            return TestResult.Skipped(TestId.EnvFirewall, Strings.Get("Env_FirewallUnreadable"));

        bool allow = false, block = false;
        foreach (var name in key.GetValueNames())
        {
            var rule = key.GetValue(name)?.ToString() ?? string.Empty;
            if (!rule.Contains("rigctld", StringComparison.OrdinalIgnoreCase)) continue;
            if (!rule.Contains("|Active=TRUE|", StringComparison.OrdinalIgnoreCase)) continue;
            if (rule.Contains("|Action=Block|", StringComparison.OrdinalIgnoreCase)) block = true;
            if (rule.Contains("|Action=Allow|", StringComparison.OrdinalIgnoreCase)) allow = true;
        }

        if (block)
        {
            return TestResult.Warning(TestId.EnvFirewall, Strings.Get("Env_FirewallBlock"), string.Empty) with
            {
                Diagnosis = Diag("Env_FirewallBlock", 2),
            };
        }

        return TestResult.Pass(TestId.EnvFirewall,
            Strings.Get(allow ? "Env_FirewallAllow" : "Env_FirewallNone"), string.Empty);
    }

    // ── Installed radio software ──────────────────────────────────────────

    private static readonly string[] KnownApps =
        ["WSJT-X", "Fldigi", "JS8Call", "Winlink Express", "RMS Express", "Flrig", "Hamlib"];

    private static TestResult CheckRadioApps()
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var roots = new (RegistryKey Root, string Sub)[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (root, sub) in roots)
        {
            using var key = root.OpenSubKey(sub);
            if (key is null) continue;
            foreach (var subName in key.GetSubKeyNames())
            {
                using var app = key.OpenSubKey(subName);
                var display = app?.GetValue("DisplayName") as string;
                if (string.IsNullOrEmpty(display)) continue;
                if (!KnownApps.Any(k => display.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
                var version = app?.GetValue("DisplayVersion") as string;
                found.Add(string.IsNullOrEmpty(version) ? display : $"{display} {version}");
            }
        }

        return found.Count == 0
            ? TestResult.Pass(TestId.EnvRadioApps, Strings.Get("Env_AppsNone"), string.Empty)
            : TestResult.Pass(TestId.EnvRadioApps, string.Join(", ", found), string.Empty);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Build a diagnosis from resource keys "{key}_Summary" and "{key}_1..n",
    /// the same layout DiagnosisEngine uses.
    /// </summary>
    private static DiagnosticResult Diag(string key, int checks, string? fixCommand = null, string? learnMore = null) =>
        new(
            Summary:      Strings.Get($"{key}_Summary"),
            Checks:       Enumerable.Range(1, checks).Select(i => Strings.Get($"{key}_{i}")).ToArray(),
            FixCommand:   fixCommand,
            LearnMoreUrl: learnMore);
}
