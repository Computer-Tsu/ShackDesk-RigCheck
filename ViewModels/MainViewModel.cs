using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RigCheck.Localization;
using RigCheck.Models;
using RigCheck.Services;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows;

namespace RigCheck.ViewModels;

/// <summary>
/// Top-level ViewModel for RigCheck's main window.
/// Coordinates the connection configuration, test runner,
/// results display, and raw command console.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly TestRunnerService   _testRunner;
    private readonly EnvironmentCheckService _envCheck;
    private readonly DiscoveryEngine     _discovery;
    private readonly ConfigClueService   _clues;
    private readonly HandoffBuilder      _handoff;
    private readonly LogExportService    _logExport;
    private readonly HamlibLocatorService _hamlib;
    private readonly SettingsService     _settings;
    private readonly TelemetryService    _telemetry;

    public ConnectionViewModel  Connection  { get; }
    public TestResultsViewModel Results     { get; }
    public RawConsoleViewModel  RawConsole  { get; }

    // ── Observable state ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunTestsCommand), nameof(ScanEnvironmentCommand), nameof(FindRadioCommand))]
    private bool   _isRunning;
    [ObservableProperty] private bool   _rawConsoleVisible;
    [ObservableProperty] private bool   _chromeVisible;
    [ObservableProperty] private bool   _alwaysOnTop;
    [ObservableProperty] private double _scaleFactor = 1.0;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _hamlibStatus  = string.Empty;

    public bool IsHamlibAvailable => _hamlib.IsAvailable;
    public bool IsHamlibMissing   => !_hamlib.IsAvailable;

    /// <summary>
    /// Raised when a test run (later: a scan) has finished, with true when
    /// nothing failed. The window uses it to flash the taskbar button or play
    /// a sound — view concerns the ViewModel stays out of.
    /// </summary>
    public event Action<bool>? TaskCompleted;

    // Title shown in window chrome. Alpha and beta builds always show the
    // expiry date here so it is visible without opening any dialog.
    public string WindowTitle =>
        BuildInfo.ExpiryDate is { } exp
            ? $"{BrandingInfo.FullName}  {BuildInfo.VersionLabel}  —  {Strings.Format("Expiry_TitleBar", exp.ToString("yyyy-MM-dd"))}"
            : $"{BrandingInfo.FullName}  {BuildInfo.VersionLabel}";

    // ── Constructor ───────────────────────────────────────────────────────

    public MainViewModel(
        ConnectionViewModel  connection,
        TestResultsViewModel results,
        RawConsoleViewModel  rawConsole,
        TestRunnerService    testRunner,
        EnvironmentCheckService envCheck,
        DiscoveryEngine      discovery,
        ConfigClueService    clues,
        HandoffBuilder       handoff,
        LogExportService     logExport,
        HamlibLocatorService hamlib,
        SettingsService      settings,
        TelemetryService     telemetry)
    {
        Connection  = connection;
        Results     = results;
        RawConsole  = rawConsole;
        _testRunner = testRunner;
        _envCheck   = envCheck;
        _discovery  = discovery;
        _clues      = clues;
        _handoff    = handoff;
        _logExport  = logExport;
        _hamlib     = hamlib;
        _settings   = settings;
        _telemetry  = telemetry;

        LoadSettings();
        CheckHamlib();

        // Run Tests stays disabled until a radio and port are chosen; the
        // status strip explains what is still missing.
        Connection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ConnectionViewModel.IsReady)
                               or nameof(ConnectionViewModel.ReadinessHint))
            {
                RunTestsCommand.NotifyCanExecuteChanged();
                UpdateReadinessStatus();
            }
        };
        UpdateReadinessStatus();
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRunTests))]
    private async Task RunTestsAsync()
    {
        IsRunning = true;
        StatusMessage = Strings.Get("Status_Running");
        Results.Clear(TestRunnerService.SuiteTests);
        CopyResultsCommand.NotifyCanExecuteChanged();

        var cfg = Connection.BuildConfig();

        // Confirm before set-frequency test (transmitter-adjacent)
        bool runSetFreq = false;
        if (_settings.Current.RunSetFreqTest)
        {
            runSetFreq = MessageBox.Show(
                Strings.Get("Confirm_SetFreq_Message"),
                Strings.Get("Confirm_SetFreq_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        try
        {
            var progress = new Progress<TestResult>(result =>
                Application.Current.Dispatcher.Invoke(() => Results.AddResult(result)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var suite = await _testRunner.RunAllAsync(cfg, runSetFreq, progress, cts.Token);

            Results.SetSuiteResult(suite);
            TaskCompleted?.Invoke(suite.AllPassed);
            StatusMessage = suite.AllPassed
                ? Strings.Format("Status_AllPassed", suite.PassCount)
                : Strings.Format("Status_SomeFailed", suite.FailCount);

            Log.Information("Test suite complete: {Pass} pass, {Fail} fail, {Warn} warn",
                suite.PassCount, suite.FailCount, suite.WarningCount);

            // Fire-and-forget; the report must never delay showing results.
            _ = _telemetry.ReportTestRunAsync(suite, Connection.SelectedPort);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("Status_RunFailed");
            Log.Error(ex, "Test suite threw an exception");
        }
        finally
        {
            IsRunning = false;
            CopyResultsCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunTests() => !IsRunning && _hamlib.IsAvailable && Connection.IsReady;

    // Environment scan: PC-side checks only, no radio needed, so it is not
    // gated on readiness — it is the thing to run when Run Tests is greyed
    // out and the operator wants to know why. Always operator-initiated.
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanEnvironmentAsync()
    {
        IsRunning = true;
        StatusMessage = Strings.Get("Status_Scanning");
        Results.Clear(EnvironmentCheckService.Checks);
        CopyResultsCommand.NotifyCanExecuteChanged();

        try
        {
            var progress = new Progress<TestResult>(result =>
                Application.Current.Dispatcher.Invoke(() => Results.AddResult(result)));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var suite = await _envCheck.RunAllAsync(Connection.BuildConfig(), progress, cts.Token);

            Results.SetSuiteResult(suite);
            TaskCompleted?.Invoke(suite.AllPassed);
            StatusMessage = Strings.Format("Status_ScanDone", suite.WarningCount + suite.FailCount);

            Log.Information("Environment scan complete: {Pass} ok, {Fail} fail, {Warn} warn",
                suite.PassCount, suite.FailCount, suite.WarningCount);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("Status_RunFailed");
            Log.Error(ex, "Environment scan threw an exception");
        }
        finally
        {
            IsRunning = false;
            CopyResultsCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanScan() => !IsRunning;

    // ── Find my radio ─────────────────────────────────────────────────────
    // The window collects the ticked ports in a dialog and calls this. The
    // engine's events become transcript lines as they arrive; each rig it
    // finds is then verified with the ordinary test suite, and the best
    // verified one is written into the Connection panel.

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task FindRadioAsync(IReadOnlyList<ComPortInfo> ports)
    {
        IsRunning = true;
        StatusMessage = Strings.Get("Status_Discovering");
        Results.Clear([]);
        CopyResultsCommand.NotifyCanExecuteChanged();

        var found    = new List<DiscoveredRig>();
        var verified = new List<(DiscoveredRig Rig, TestSuiteResult Suite)>();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            // What the operator's own programs are set to is the best first
            // guess: their port is probed first, and their radio stands in
            // for the Connection panel when nothing is chosen there.
            var clues = _clues.ReadAll();
            foreach (var clue in clues)
                Results.AddTranscript(TranscriptKind.Note, HandoffBuilder.Describe(clue));

            var orderedPorts = ports
                .OrderByDescending(p => clues.Any(c => c.Port.Equals(p.PortName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var preferredModel = Connection.ModelId > 1
                ? Connection.ModelId
                : clues.FirstOrDefault(c => c.ModelId > 1)?.ModelId ?? 0;

            var request = new DiscoveryRequest(orderedPorts, preferredModel, Connection.BaudRate, Clues: clues);

            Results.AddTranscript(TranscriptKind.Note, Strings.Format("Disc_Start", ports.Count));

            await foreach (var ev in _discovery.RunAsync(request, cts.Token))
            {
                Results.AddTranscript(ev.Kind switch
                {
                    ProbeEventKind.Sent     => TranscriptKind.SerialTx,
                    ProbeEventKind.Received => TranscriptKind.SerialRx,
                    ProbeEventKind.Trying   => TranscriptKind.Trying,
                    ProbeEventKind.Found    => TranscriptKind.Found,
                    ProbeEventKind.Response => TranscriptKind.Response,
                    _                       => TranscriptKind.Note,
                }, ev.Bytes ?? ev.Message);

                if (ev.Rig is not null) found.Add(ev.Rig);
            }

            // ── Stage 3: verify every find with the real test suite ───────
            foreach (var rig in found)
            {
                Results.AddTranscript(TranscriptKind.Command,
                    Strings.Format("Disc_Verifying", rig.ModelName, rig.Port, rig.Baud));

                var cfg = rig.UseRigctld
                    ? new ConnectionConfig { UseRigctld = true, RigctldHost = rig.Port.Split(':')[0],
                                             RigctldPort = int.TryParse(rig.Port.Split(':').Last(), out var p) ? p : BrandingInfo.DefaultRigctldPort,
                                             ModelId = rig.HamlibModelId, RadioModelName = rig.ModelName }
                    : new ConnectionConfig { ModelId = rig.HamlibModelId, RadioModelName = rig.ModelName,
                                             ComPort = rig.Port, BaudRate = rig.Baud };

                var progress = new Progress<TestResult>(r =>
                    Application.Current.Dispatcher.Invoke(() => Results.AddResult(r)));
                var suite = await _testRunner.RunAllAsync(cfg, false, progress, cts.Token);
                if (suite.AllPassed) verified.Add((rig, suite));

                Results.AddTranscript(suite.AllPassed ? TranscriptKind.Found : TranscriptKind.Note,
                    suite.AllPassed ? Strings.Format("Disc_Verified", rig.ModelName, rig.Port)
                                    : Strings.Format("Disc_VerifyFailed", rig.ModelName, rig.Port, suite.FailCount));
            }

            // ── Stage 4 (first cut): hand the best one to the Connection panel
            if (verified.Count > 0)
            {
                var best = verified.OrderByDescending(v => v.Rig.Score).First();
                Connection.ApplyDiscovered(best.Rig);
                Results.SetSuiteResult(best.Suite);
                Results.AddTranscript(TranscriptKind.Found,
                    Strings.Format("Disc_Applied", best.Rig.ModelName, best.Rig.HamlibModelId, best.Rig.Port, best.Rig.Baud));

                // Stage 4: the settings to type into each program, and how
                // they differ from what those programs use today.
                foreach (var line in _handoff.Build(best.Rig, clues))
                    Results.AddTranscript(line.Kind, line.Text);
                StatusMessage = verified.Count == 1
                    ? Strings.Format("Status_DiscoveredOne", best.Rig.ModelName, best.Rig.Port)
                    : Strings.Format("Status_DiscoveredMany", verified.Count, best.Rig.ModelName, best.Rig.Port);
            }
            else
            {
                Results.SetSuiteResult(new TestSuiteResult([], Connection.BuildConfig()));
                StatusMessage = found.Count > 0 ? Strings.Get("Status_DiscoveredUnverified") : Strings.Get("Status_DiscoveredNone");
                Results.AddTranscript(TranscriptKind.Note, StatusMessage);
            }

            TaskCompleted?.Invoke(verified.Count > 0);
            Log.Information("Discovery complete: {Found} found, {Verified} verified", found.Count, verified.Count);
            _ = _telemetry.ReportDiscoveryAsync(ports.Count, found, verified.Select(v => v.Rig).ToList());
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.Get("Status_DiscoveryTimeout");
            Results.AddTranscript(TranscriptKind.Note, StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = Strings.Get("Status_RunFailed");
            Log.Error(ex, "Discovery threw an exception");
        }
        finally
        {
            IsRunning = false;
            CopyResultsCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        if (Results.SuiteResult is null)
        {
            MessageBox.Show(Strings.Get("Export_NoResults_Message"),
                Strings.Get("Export_NoResults_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = Strings.Format("Export_DialogTitle", BrandingInfo.AppName),
            Filter     = Strings.Get("Export_Filter"),
            FileName   = $"{BrandingInfo.AppName}-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExt = ".txt",
        };

        if (dlg.ShowDialog() != true) return;

        var path = await _logExport.ExportAsync(Results.SuiteResult, dlg.FileName, Results.Transcript);

        StatusMessage = path is not null
            ? Strings.Format("Status_LogSaved", path)
            : Strings.Get("Status_LogFailed");
    }

    // The same plain-text report as Export Log, straight to the clipboard.
    [RelayCommand(CanExecute = nameof(HasResults))]
    private void CopyResults()
    {
        if (Results.SuiteResult is null) return;
        Clipboard.SetText(_logExport.BuildReport(Results.SuiteResult, Results.Transcript));
        StatusMessage = Strings.Get("Status_Copied");
    }

    private bool HasResults() => Results.SuiteResult is not null;

    [RelayCommand]
    private Task SendRawCommandAsync() =>
        RawConsole.SendCommandCommand.ExecuteAsync(Connection.BuildConfig());

    [RelayCommand]
    private void OpenHamlibDownload() => OpenUrl(BrandingInfo.HamlibDownloadUrl);

    [RelayCommand]
    private void OpenHelp() => OpenUrl(BrandingInfo.HelpUrl);

    [RelayCommand]
    private void OpenReleases() => OpenUrl(BrandingInfo.ReleasesUrl);

    private static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = url,
            UseShellExecute = true,
        });

    [RelayCommand]
    private void ToggleRawConsole() =>
        RawConsoleVisible = !RawConsoleVisible;

    [RelayCommand]
    private void ToggleChrome() =>
        ChromeVisible = !ChromeVisible;

    [RelayCommand]
    private void ScaleUp()   => ScaleFactor = Math.Min(ScaleFactor + 0.1, 2.0);

    [RelayCommand]
    private void ScaleDown() => ScaleFactor = Math.Max(ScaleFactor - 0.1, 0.7);

    [RelayCommand]
    private void ScaleReset() => ScaleFactor = 1.0;

    // ── Window lifecycle ──────────────────────────────────────────────────

    public void OnWindowClosing(double windowLeft, double windowTop)
    {
        _settings.Update(s =>
        {
            s.RawConsoleOpen = RawConsoleVisible;
            s.ScaleFactor    = ScaleFactor;
            s.AlwaysOnTop    = AlwaysOnTop;
            s.WindowLeft     = windowLeft;
            s.WindowTop      = windowTop;
            s.CommandHistory = RawConsole.GetHistory();
            Connection.SaveTo(s);
        });
    }

    // ── Init helpers ──────────────────────────────────────────────────────

    private void LoadSettings()
    {
        var s = _settings.Current;
        RawConsoleVisible = s.RawConsoleOpen;
        ScaleFactor       = s.ScaleFactor;
        AlwaysOnTop       = s.AlwaysOnTop;
        RawConsole.LoadHistory(s.CommandHistory);
        Connection.LoadFrom(s);
    }

    private void CheckHamlib()
    {
        if (_hamlib.IsAvailable)
        {
            HamlibStatus = Strings.Format("Status_HamlibFound", _hamlib.FoundVia ?? string.Empty);
            Log.Information("Hamlib available at {Path}", _hamlib.RigctlPath);
        }
        else
        {
            HamlibStatus = Strings.Get("Status_HamlibMissing");
            Log.Warning("Hamlib not available");
        }

        RunTestsCommand.NotifyCanExecuteChanged();
    }

    // Status strip shows the single most important message. An imminent
    // expiry outranks everything because nothing else matters once the
    // build stops running.
    private void UpdateReadinessStatus()
    {
        if (IsRunning) return;

        StatusMessage = ExpiryMessage()
            ?? (!_hamlib.IsAvailable
                ? Strings.Get("Status_HamlibMissingLong")
                : Connection.ReadinessHint);
    }

    private static string? ExpiryMessage()
    {
        if (BuildInfo.ExpiryDate is not { } exp) return null;
        var date = exp.ToString("yyyy-MM-dd");

        if (BuildInfo.IsExpired)
            return BuildInfo.IsBeta ? Strings.Format("Expiry_BetaExpired", date) : null;

        if (!BuildInfo.IsExpiringSoon) return null;

        var days = BuildInfo.DaysUntilExpiry ?? 0;
        return days == 0
            ? Strings.Format("Expiry_WarningToday", BuildInfo.Channel)
            : Strings.Format("Expiry_Warning", BuildInfo.Channel, days, date);
    }
}
