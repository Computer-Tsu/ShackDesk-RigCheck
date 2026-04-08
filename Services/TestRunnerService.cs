using RigCheck.Models;
using Serilog;

namespace RigCheck.Services;

/// <summary>
/// Orchestrates the RigCheck diagnostic test suite.
/// Runs each test in order, builds human-readable results,
/// and attaches the rigctl command for every test so the
/// user can copy and run it themselves.
/// </summary>
public class TestRunnerService
{
    private readonly HamlibRunnerService _runner;
    private readonly RigctlCommandBuilder _builder;
    private readonly DiagnosisEngine _diagnosis;

    public TestRunnerService(
        HamlibRunnerService runner,
        RigctlCommandBuilder builder,
        DiagnosisEngine diagnosis)
    {
        _runner    = runner;
        _builder   = builder;
        _diagnosis = diagnosis;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Run the full diagnostic test suite.
    /// Progress is reported via the <paramref name="progress"/> callback
    /// so the UI can update results in real time as each test completes.
    /// </summary>
    public async Task<TestSuiteResult> RunAllAsync(
        ConnectionConfig cfg,
        bool includeSetFreqTest,
        IProgress<TestResult>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<TestResult>();

        async Task<TestResult> Run(TestId id, Func<Task<TestResult>> test)
        {
            if (ct.IsCancellationRequested)
                return TestResult.Skipped(id, "Cancelled");

            Log.Information("Running test: {TestId}", id);
            var result = await test();
            results.Add(result);
            progress?.Report(result);
            return result;
        }

        // Test 1 — Open connection
        var t1 = await Run(TestId.OpenConnection, () => TestOpenConnectionAsync(cfg, ct));

        // Stop suite if we can't connect at all
        if (t1.Status == TestStatus.Fail)
        {
            Log.Warning("Connection failed — skipping remaining tests");
            var skipped = RemainingTests()
                .Select(id => TestResult.Skipped(id, "Skipped — connection could not be established"))
                .ToList();
            foreach (var s in skipped) progress?.Report(s);
            results.AddRange(skipped);
            return new TestSuiteResult(results, cfg);
        }

        // Tests 2–6 — standard queries
        await Run(TestId.GetFrequency, () => TestGetFrequencyAsync(cfg, ct));
        await Run(TestId.GetMode,      () => TestGetModeAsync(cfg, ct));
        await Run(TestId.GetPtt,       () => TestGetPttAsync(cfg, ct));
        await Run(TestId.GetSmeter,    () => TestGetSmeterAsync(cfg, ct));
        await Run(TestId.GetVfo,       () => TestGetVfoAsync(cfg, ct));

        // Test 7 — optional set frequency (user must confirm before calling this)
        if (includeSetFreqTest)
            await Run(TestId.SetFrequency, () => TestSetFrequencyAsync(cfg, ct));

        return new TestSuiteResult(results, cfg);
    }

    // ── Individual tests ──────────────────────────────────────────────────

    private async Task<TestResult> TestOpenConnectionAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.TestConnection(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess)
            return TestResult.Pass(
                TestId.OpenConnection,
                $"Connected to {cfg.RadioModelName} on {cfg.ComPort}",
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.OpenConnection,
            $"Could not connect to radio",
            cmd.DisplayCommand,
            diag);
    }

    private async Task<TestResult> TestGetFrequencyAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetFrequency(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess && TryParseFrequency(result.RawOutput, out var mhz))
            return TestResult.Pass(
                TestId.GetFrequency,
                $"VFO A frequency: {mhz:F3} MHz",
                cmd.DisplayCommand);

        if (result.IsSuccess)
            return TestResult.Warning(
                TestId.GetFrequency,
                $"Radio responded but frequency could not be parsed: {result.RawOutput}",
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.GetFrequency,
            "Radio did not respond to frequency query",
            cmd.DisplayCommand,
            diag);
    }

    private async Task<TestResult> TestGetModeAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetMode(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess)
        {
            var lines = result.RawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var mode  = lines.ElementAtOrDefault(0)?.Trim() ?? result.RawOutput;
            var pb    = lines.ElementAtOrDefault(1)?.Trim() ?? "?";
            return TestResult.Pass(
                TestId.GetMode,
                $"Current mode: {mode}, passband: {pb} Hz",
                cmd.DisplayCommand);
        }

        return TestResult.Fail(
            TestId.GetMode,
            "Radio did not respond to mode query",
            cmd.DisplayCommand,
            diag);
    }

    private async Task<TestResult> TestGetPttAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetPtt(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess)
        {
            var pttOn = result.RawOutput.Trim() == "1";
            var label = pttOn
                ? "PTT is ON — transmitter is KEYED"
                : "PTT is off (transmitter not keyed)";
            return TestResult.Pass(TestId.GetPtt, label, cmd.DisplayCommand);
        }

        return TestResult.Fail(
            TestId.GetPtt,
            "Radio did not respond to PTT query",
            cmd.DisplayCommand,
            diag);
    }

    private async Task<TestResult> TestGetSmeterAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetSmeter(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess && TryParseSmeter(result.RawOutput, out var sLabel, out var dbm))
            return TestResult.Pass(
                TestId.GetSmeter,
                $"Signal strength: {sLabel} ({dbm} dBm)",
                cmd.DisplayCommand);

        if (result.IsSuccess)
            return TestResult.Warning(
                TestId.GetSmeter,
                $"S-meter responded but value could not be parsed: {result.RawOutput}",
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.GetSmeter,
            "Radio did not respond to S-meter query (some radios don't support this)",
            cmd.DisplayCommand,
            diag);
    }

    private async Task<TestResult> TestGetVfoAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetVfo(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess)
            return TestResult.Pass(
                TestId.GetVfo,
                $"Active VFO: {result.RawOutput.Trim()}",
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.GetVfo,
            "Radio did not respond to VFO query",
            cmd.DisplayCommand,
            diag);
    }

    private async Task<TestResult> TestSetFrequencyAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        // Read current frequency first
        var getCmd = RigctlCommandBuilder.GetFrequency(cfg);
        var getResult = await _runner.RunAsync(getCmd, ct);
        if (!getResult.IsSuccess || !TryParseFrequency(getResult.RawOutput, out var currentMhz))
        {
            return TestResult.Fail(
                TestId.SetFrequency,
                "Could not read current frequency before set test",
                getCmd.DisplayCommand,
                _diagnosis.Diagnose(getResult, cfg));
        }

        // Offset by +1 kHz for the test
        var testHz = (long)((currentMhz + 0.001) * 1_000_000);
        var setCmd = RigctlCommandBuilder.SetFrequency(cfg, testHz);
        var setResult = await _runner.RunAsync(setCmd, ct);

        if (!setResult.IsSuccess)
        {
            return TestResult.Fail(
                TestId.SetFrequency,
                "Failed to set frequency",
                setCmd.DisplayCommand,
                _diagnosis.Diagnose(setResult, cfg));
        }

        // Read back and verify
        await Task.Delay(200, ct);  // brief settle time
        var verifyResult = await _runner.RunAsync(getCmd, ct);
        if (verifyResult.IsSuccess && TryParseFrequency(verifyResult.RawOutput, out var verifyMhz))
        {
            var diff = Math.Abs(verifyMhz - (currentMhz + 0.001));
            if (diff < 0.0005) // within 500 Hz
            {
                // Restore original frequency
                var restoreCmd = RigctlCommandBuilder.SetFrequency(cfg, (long)(currentMhz * 1_000_000));
                await _runner.RunAsync(restoreCmd, ct);

                return TestResult.Pass(
                    TestId.SetFrequency,
                    $"Set frequency verified — set {testHz / 1_000_000.0:F3} MHz, read back {verifyMhz:F3} MHz. Original frequency restored.",
                    setCmd.DisplayCommand);
            }
        }

        return TestResult.Warning(
            TestId.SetFrequency,
            "Frequency was set but readback did not match exactly",
            setCmd.DisplayCommand);
    }

    // ── Parsers ───────────────────────────────────────────────────────────

    private static bool TryParseFrequency(string raw, out double mhz)
    {
        mhz = 0;
        if (double.TryParse(raw.Trim(), out var hz) && hz > 0)
        {
            mhz = hz / 1_000_000.0;
            return true;
        }
        return false;
    }

    private static bool TryParseSmeter(string raw, out string sLabel, out string dbm)
    {
        sLabel = "?"; dbm = "?";
        if (!double.TryParse(raw.Trim(), out var val)) return false;

        // Hamlib returns S-meter in dBm-ish units (actual values depend on radio)
        // Standard S-unit: S9 = -73 dBm, each S unit = 6 dB below
        dbm = $"{val:F0}";
        var sUnits = Math.Clamp((int)Math.Round((val + 127.0) / 6.0), 0, 9);
        sLabel = sUnits >= 9 ? $"S9+{(int)(val + 73)}" : $"S{sUnits}";
        return true;
    }

    private static IEnumerable<TestId> RemainingTests() =>
    [
        TestId.GetFrequency, TestId.GetMode, TestId.GetPtt,
        TestId.GetSmeter, TestId.GetVfo, TestId.SetFrequency
    ];
}
