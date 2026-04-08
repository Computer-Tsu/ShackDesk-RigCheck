using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly LogExportService    _logExport;
    private readonly HamlibLocatorService _hamlib;
    private readonly SettingsService     _settings;

    public ConnectionViewModel  Connection  { get; }
    public TestResultsViewModel Results     { get; }
    public RawConsoleViewModel  RawConsole  { get; }

    // ── Observable state ─────────────────────────────────────────────────

    [ObservableProperty] private bool   _isRunning;
    [ObservableProperty] private bool   _rawConsoleVisible;
    [ObservableProperty] private bool   _alwaysOnTop;
    [ObservableProperty] private double _scaleFactor = 1.0;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _hamlibStatus  = string.Empty;

    // Title shown in window chrome
    public string WindowTitle =>
        $"{BrandingInfo.FullName}  {BrandingInfo.Version}";

    // ── Constructor ───────────────────────────────────────────────────────

    public MainViewModel(
        ConnectionViewModel  connection,
        TestResultsViewModel results,
        RawConsoleViewModel  rawConsole,
        TestRunnerService    testRunner,
        LogExportService     logExport,
        HamlibLocatorService hamlib,
        SettingsService      settings)
    {
        Connection  = connection;
        Results     = results;
        RawConsole  = rawConsole;
        _testRunner = testRunner;
        _logExport  = logExport;
        _hamlib     = hamlib;
        _settings   = settings;

        LoadSettings();
        CheckHamlib();
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRunTests))]
    private async Task RunTestsAsync()
    {
        IsRunning = true;
        StatusMessage = "Running tests…";
        Results.Clear();

        var cfg = Connection.BuildConfig();

        // Confirm before set-frequency test (transmitter-adjacent)
        bool runSetFreq = false;
        if (_settings.Current.RunSetFreqTest)
        {
            runSetFreq = MessageBox.Show(
                "The Set Frequency test will briefly change your radio's VFO frequency " +
                "by 1 kHz, then restore it.\n\nContinue?",
                "Confirm frequency test",
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
            StatusMessage = suite.AllPassed
                ? $"All {suite.PassCount} tests passed."
                : $"{suite.FailCount} test(s) failed — see results for details.";

            Log.Information("Test suite complete: {Pass} pass, {Fail} fail, {Warn} warn",
                suite.PassCount, suite.FailCount, suite.WarningCount);
        }
        catch (Exception ex)
        {
            StatusMessage = "Test run failed unexpectedly.";
            Log.Error(ex, "Test suite threw an exception");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRunTests() => !IsRunning && _hamlib.IsAvailable;

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        if (Results.SuiteResult is null)
        {
            MessageBox.Show("Run the tests first before exporting.",
                "No results", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Export RigCheck log",
            Filter     = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName   = $"RigCheck-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExt = ".txt",
        };

        if (dlg.ShowDialog() != true) return;

        var path = await _logExport.ExportAsync(Results.SuiteResult, dlg.FileName);

        if (path is not null)
            StatusMessage = $"Log saved to {path}";
        else
            StatusMessage = "Log export failed — check the application log for details.";
    }

    [RelayCommand]
    private void ToggleRawConsole() =>
        RawConsoleVisible = !RawConsoleVisible;

    [RelayCommand]
    private void ScaleUp()   => ScaleFactor = Math.Min(ScaleFactor + 0.1, 2.0);

    [RelayCommand]
    private void ScaleDown() => ScaleFactor = Math.Max(ScaleFactor - 0.1, 0.7);

    [RelayCommand]
    private void ScaleReset() => ScaleFactor = 1.0;

    // ── Window lifecycle ──────────────────────────────────────────────────

    public void OnWindowClosing()
    {
        _settings.Update(s =>
        {
            s.RawConsoleOpen = RawConsoleVisible;
            s.ScaleFactor    = ScaleFactor;
            s.AlwaysOnTop    = AlwaysOnTop;
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
        Connection.LoadFrom(s);
    }

    private void CheckHamlib()
    {
        if (_hamlib.IsAvailable)
        {
            HamlibStatus = $"Hamlib found via {_hamlib.FoundVia}";
            Log.Information("Hamlib available at {Path}", _hamlib.RigctlPath);
        }
        else
        {
            HamlibStatus = "Hamlib not found — install WSJT-X or download Hamlib";
            StatusMessage = "Hamlib not found. Run Tests will be unavailable until Hamlib is installed.";
            Log.Warning("Hamlib not available");
        }

        RunTestsCommand.NotifyCanExecuteChanged();
    }
}
