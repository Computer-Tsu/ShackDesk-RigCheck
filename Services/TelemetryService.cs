using RigCheck.Models;
using Serilog;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RigCheck.Services;

/// <summary>
/// Sends anonymous diagnostic reports to the ShackDesk telemetry endpoint.
/// Same payload shape and endpoint as PortPane, so both apps land in the
/// same database and dashboard.
///
/// Rules, in order of importance:
///   - Nothing is sent unless the operator enabled it (first-run prompt or Settings).
///   - Nothing here ever blocks the UI or a test run. Sends are fire-and-forget
///     with a short timeout; failures are queued to disk and retried at next startup.
///   - Every payload sent or queued is also written locally so the operator can
///     see exactly what left the machine (Help > View collected data).
///   - No callsign, machine name, file path, serial number, or IP address is ever
///     included. The only identifier is a random install ID, so support requests
///     can be matched to reports if the operator chooses to quote it.
/// </summary>
public sealed class TelemetryService
{
    private readonly HttpClient      _http;
    private readonly SettingsService _settings;

    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        BrandingInfo.SuiteName, BrandingInfo.AppName, "Telemetry");

    private const int    MaxPending  = 10;
    private const int    MaxSentKept = 50;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public TelemetryService(HttpClient http, SettingsService settings)
    {
        _http     = http;
        _settings = settings;
        Directory.CreateDirectory(DataDir);
    }

    public bool IsEnabled => _settings.Current.TelemetryEnabled;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Sent once per launch: which channel, and whether Hamlib was found.</summary>
    public Task ReportStartupAsync(HamlibLocatorService hamlib) =>
        ReportAsync("startup", new Dictionary<string, object?>
        {
            ["channel"]      = BuildInfo.Channel,
            ["hamlib_found"] = hamlib.IsAvailable,
            ["hamlib_via"]   = hamlib.FoundVia,
        });

    /// <summary>
    /// Sent after a test run. This is the report that improves the radio
    /// presets database: which cable, which model, which settings, and
    /// which tests passed or failed.
    /// </summary>
    public Task ReportTestRunAsync(TestSuiteResult suite, ComPortInfo? port) =>
        ReportAsync("test_run", new Dictionary<string, object?>
        {
            ["model_id"]     = suite.Config.ModelId,
            ["model_name"]   = suite.Config.RadioModelName,
            ["baud"]         = suite.Config.BaudRate,
            ["use_rigctld"]  = suite.Config.UseRigctld,
            ["port_vid"]     = port?.Vid,
            ["port_pid"]     = port?.Pid,
            ["cable_family"] = port?.RadioFamily,
            ["passed"]       = suite.PassCount,
            ["failed"]       = suite.FailCount,
            ["results"]      = suite.Results.Select(r => new
            {
                test   = r.Id.ToString(),
                status = r.Status.ToString(),
                error  = r.Error?.ToString(),
            }),
        });

    /// <summary>
    /// Outcome of a Find my radio run: how many ports were swept and what
    /// answered. Model, family, baud, and score per find — this is the data
    /// that improves the family weights and baud orders in rig_families.json.
    /// No port names or bytes.
    /// </summary>
    public Task ReportDiscoveryAsync(int portsSwept, IReadOnlyList<DiscoveredRig> found, IReadOnlyList<DiscoveredRig> verified) =>
        ReportAsync("discovery", new Dictionary<string, object?>
        {
            ["ports_swept"] = portsSwept,
            ["found"]       = found.Count,
            ["verified"]    = verified.Count,
            ["rigs"]        = found.Select(r => new
            {
                family   = r.FamilyId,
                model_id = r.HamlibModelId,
                baud     = r.Baud,
                score    = r.Score,
                verified = verified.Contains(r),
            }),
        });

    /// <summary>Exception type and message only — never the stack trace, which can contain file paths.</summary>
    public Task ReportCrashAsync(Exception ex) =>
        ReportAsync("crash", new Dictionary<string, object?>
        {
            ["exception"] = ex.GetType().Name,
            ["message"]   = ex.Message,
        });

    // ── Pending queue ─────────────────────────────────────────────────────

    /// <summary>Retry reports that could not be sent earlier. Called at startup, never awaited by the UI.</summary>
    public async Task FlushPendingAsync()
    {
        if (!IsEnabled) return;

        foreach (var file in Directory.GetFiles(DataDir, "pending-*.json").OrderBy(f => f))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                if (await PostAsync(json))
                {
                    File.Move(file, file.Replace("pending-", "sent-"), overwrite: true);
                    Log.Debug("Telemetry: delivered queued report {File}", Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Telemetry: could not flush {File}", Path.GetFileName(file));
            }
        }
    }

    /// <summary>Every report written locally, newest first, for the data viewer.</summary>
    public IReadOnlyList<(string Name, string Json)> GetLocalReports() =>
        Directory.GetFiles(DataDir, "*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => (f.Name, SafeRead(f.FullName)))
            .ToList();

    public void ClearLocalReports()
    {
        foreach (var f in Directory.GetFiles(DataDir, "*.json"))
            try { File.Delete(f); } catch { /* best effort */ }
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private async Task ReportAsync(string eventName, Dictionary<string, object?> props)
    {
        if (!IsEnabled) return;

        props["install_id"] = _settings.Current.InstallId;

        var id   = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(new
        {
            report_id = id,
            app       = BrandingInfo.AppName,
            version   = BuildInfo.VersionLabel,
            @event    = eventName,
            os        = Environment.OSVersion.VersionString,
            timestamp = DateTimeOffset.UtcNow,
            props,
        }, JsonOpts);

        var delivered = await PostAsync(json);
        WriteLocal(delivered ? $"sent-{id}.json" : $"pending-{id}.json", json);
        if (delivered) PruneSent(); else PrunePending();
    }

    private async Task<bool> PostAsync(string json)
    {
        try
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(BrandingInfo.TelemetryEndpoint, body);
            Log.Debug("Telemetry: {Status}", resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // Offline is the normal case in the field — quiet at Debug level.
            Log.Debug(ex, "Telemetry: send failed, queuing");
            return false;
        }
    }

    private static void WriteLocal(string name, string json)
    {
        try { File.WriteAllText(Path.Combine(DataDir, name), json); }
        catch (Exception ex) { Log.Debug(ex, "Telemetry: could not write local copy"); }
    }

    private static void PrunePending() => Prune("pending-*.json", MaxPending);
    private static void PruneSent()    => Prune("sent-*.json", MaxSentKept);

    private static void Prune(string pattern, int keep)
    {
        var files = Directory.GetFiles(DataDir, pattern)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(keep);
        foreach (var f in files)
            try { f.Delete(); } catch { /* best effort */ }
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return string.Empty; }
    }
}
