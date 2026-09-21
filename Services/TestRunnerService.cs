using RigCheck.Localization;
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
    /// <summary>The connection tests, in run order — used for the pending placeholders.</summary>
    public static readonly IReadOnlyList<TestId> SuiteTests =
    [
        TestId.OpenConnection, TestId.GetFrequency, TestId.GetMode, TestId.GetPtt,
        TestId.GetSmeter, TestId.GetVfo, TestId.SetFrequency,
    ];

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
                return TestResult.Skipped(id, Strings.Get("Test_Cancelled"));

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
                .Select(id => TestResult.Skipped(id, Strings.Get("Test_SkippedNoConnection")))
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
                Strings.Format("Msg_Connected", cfg.RadioModelName, cfg.ComPort),
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.OpenConnection,
            Strings.Get("Msg_ConnectFailed"),
            cmd.DisplayCommand,
            diag, result.Error);
    }

    private async Task<TestResult> TestGetFrequencyAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetFrequency(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess && TryParseFrequency(result.RawOutput, out var mhz))
            return TestResult.Pass(
                TestId.GetFrequency,
                Strings.Format("Msg_Frequency", mhz.ToString("F3")),
                cmd.DisplayCommand);

        if (result.IsSuccess)
            return TestResult.Warning(
                TestId.GetFrequency,
                Strings.Format("Msg_FrequencyUnparsed", result.RawOutput),
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.GetFrequency,
            Strings.Get("Msg_FrequencyNoResponse"),
            cmd.DisplayCommand,
            diag, result.Error);
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
                Strings.Format("Msg_Mode", mode, pb),
                cmd.DisplayCommand);
        }

        return TestResult.Fail(
            TestId.GetMode,
            Strings.Get("Msg_ModeNoResponse"),
            cmd.DisplayCommand,
            diag, result.Error);
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
                ? Strings.Get("Msg_PttOn")
                : Strings.Get("Msg_PttOff");
            return TestResult.Pass(TestId.GetPtt, label, cmd.DisplayCommand);
        }

        return TestResult.Fail(
            TestId.GetPtt,
            Strings.Get("Msg_PttNoResponse"),
            cmd.DisplayCommand,
            diag, result.Error);
    }

    private async Task<TestResult> TestGetSmeterAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetSmeter(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess && TryParseSmeter(result.RawOutput, out var sLabel, out var dbm))
            return TestResult.Pass(
                TestId.GetSmeter,
                Strings.Format("Msg_Smeter", sLabel, dbm),
                cmd.DisplayCommand);

        if (result.IsSuccess)
            return TestResult.Warning(
                TestId.GetSmeter,
                Strings.Format("Msg_SmeterUnparsed", FirstLine(result.RawOutput)),
                cmd.DisplayCommand);

        if (result.Error == RigctlError.NotSupported)
            return TestResult.Warning(
                TestId.GetSmeter,
                Strings.Format("Msg_SmeterNotSupported", cfg.RadioModelName),
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.GetSmeter,
            Strings.Get("Msg_SmeterNoResponse"),
            cmd.DisplayCommand,
            diag, result.Error);
    }

    private async Task<TestResult> TestGetVfoAsync(ConnectionConfig cfg, CancellationToken ct)
    {
        var cmd    = RigctlCommandBuilder.GetVfo(cfg);
        var result = await _runner.RunAsync(cmd, ct);
        var diag   = _diagnosis.Diagnose(result, cfg);

        if (result.IsSuccess)
            return TestResult.Pass(
                TestId.GetVfo,
                Strings.Format("Msg_Vfo", FirstLine(result.RawOutput)),
                cmd.DisplayCommand);

        // Many Icom backends (IC-7300 included) have no get_vfo: Hamlib says
        // "Feature not available". The radio is fine; the query just does
        // not exist for it. A warning, so the run can still be all green
        // in spirit, with the reason spelled out.
        if (result.Error == RigctlError.NotSupported)
            return TestResult.Warning(
                TestId.GetVfo,
                Strings.Format("Msg_VfoNotSupported", cfg.RadioModelName),
                cmd.DisplayCommand);

        return TestResult.Fail(
            TestId.GetVfo,
            Strings.Get("Msg_VfoNoResponse"),
            cmd.DisplayCommand,
            diag, result.Error);
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
                Strings.Get("Msg_SetFreqReadFailed"),
                getCmd.DisplayCommand,
                _diagnosis.Diagnose(getResult, cfg), getResult.Error);
        }

        // Offset by +1 kHz for the test
        var testHz = (long)((currentMhz + 0.001) * 1_000_000);
        var setCmd = RigctlCommandBuilder.SetFrequency(cfg, testHz);
        var setResult = await _runner.RunAsync(setCmd, ct);

        if (!setResult.IsSuccess)
        {
            return TestResult.Fail(
                TestId.SetFrequency,
                Strings.Get("Msg_SetFreqFailed"),
                setCmd.DisplayCommand,
                _diagnosis.Diagnose(setResult, cfg), setResult.Error);
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
                    Strings.Format("Msg_SetFreqVerified", (testHz / 1_000_000.0).ToString("F3"), verifyMhz.ToString("F3")),
                    setCmd.DisplayCommand);
            }
        }

        return TestResult.Warning(
            TestId.SetFrequency,
            Strings.Get("Msg_SetFreqMismatch"),
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
        if (!double.TryParse(raw.Trim(), out var db)) return false;

        // Hamlib STRENGTH is dB relative to S9: -54 = S0, 0 = S9, +20 = S9+20.
        // One S-unit is 6 dB. S9 is -73 dBm by the HF convention.
        dbm = $"{-73 + db:F0}";
        sLabel = db >= 0
            ? (db < 1 ? "S9" : $"S9+{db:F0}")
            : $"S{Math.Clamp((int)Math.Round(9 + db / 6.0), 0, 9)}";
        return true;
    }

    /// <summary>The first non-empty line of rigctl output — the value, never a trace.</summary>
    private static string FirstLine(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

    private static IEnumerable<TestId> RemainingTests() =>
    [
        TestId.GetFrequency, TestId.GetMode, TestId.GetPtt,
        TestId.GetSmeter, TestId.GetVfo, TestId.SetFrequency
    ];
}
