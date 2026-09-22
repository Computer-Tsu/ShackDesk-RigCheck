using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RigCheck.Localization;
using RigCheck.Logging;
using RigCheck.Services;
using RigCheck.ViewModels;
using RigCheck.Views;
using Serilog;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;

namespace RigCheck;

public partial class App : Application
{
    private IHost? _host;

    // ── Single instance ───────────────────────────────────────────────────
    // One RigCheck at a time. Two windows would contend for the same COM
    // port (the second reports "in use" and blames another program) and
    // overwrite each other's settings file. A second launch signals the
    // running instance to come to the front, then exits.
    //
    // RigCheck allowed multiple instances through 0.7.0; this was changed in
    // 0.7.1 — see issue #10 for the reasoning. To allow several instances
    // again, delete this block, the two fields, and ActivateOnSignal below.
    private const string InstanceName = @"Local\ShackDesk.RigCheck";
    private static Mutex? _instanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Activate");
            signal.Set();
            Shutdown(0);
            return;
        }
        ActivateOnSignal();

        // Settings are read before the host exists because the log level lives there.
        var earlySettings = new SettingsService();
        AppLogger.Configure(AppLogger.ResolveLevel(earlySettings.Current.LogLevel));

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(s => RegisterServices(s, earlySettings))
            .Build();

        await _host.StartAsync();

        Log.Information("{App} {Version} ({Channel}, built {Built}) starting",
            BrandingInfo.AppName, BrandingInfo.Version, BuildInfo.Channel, BuildInfo.BuildDate);

        // An expired alpha shows only the expiry notice and exits. Beta builds
        // keep running past expiry and warn in the status strip instead.
        if (BuildInfo.IsExpired && BuildInfo.BlocksWhenExpired)
        {
            Log.Warning("Alpha build expired on {Expiry}; refusing to start", BuildInfo.ExpiryDate);
            new ExpiredDialog().ShowDialog();
            Shutdown();
            return;
        }

        var settings  = _host.Services.GetRequiredService<SettingsService>();
        var telemetry = _host.Services.GetRequiredService<TelemetryService>();
        Resources["Settings"] = settings;

        DispatcherUnhandledException += (_, e) => OnUnhandledException(e, telemetry);

        // Create the main window before any dialog. ShutdownMode is
        // OnMainWindowClose, and WPF treats the first window shown as the
        // main window — so a dialog shown first would end the app when closed.
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        // Ask once about anonymous diagnostics before anything is sent.
        if (!settings.Current.TelemetryPrompted)
            new FirstRunDialog(settings).ShowDialog();

        mainWindow.Show();

        // Update check after the window is up; result appears in Help and the status strip.
        _ = mainWindow.ViewModel.CheckForUpdatesQuietlyAsync();

        // Fire-and-forget: neither call may delay the window or fail loudly.
        _ = telemetry.FlushPendingAsync();
        _ = telemetry.ReportStartupAsync(_host.Services.GetRequiredService<HamlibLocatorService>());
    }

    // Log, report if allowed, tell the operator, and exit. Swallowing the
    // exception would leave the app in an unknown state.
    private void OnUnhandledException(DispatcherUnhandledExceptionEventArgs e, TelemetryService telemetry)
    {
        Log.Fatal(e.Exception, "Unhandled exception");
        try { telemetry.ReportCrashAsync(e.Exception).Wait(TimeSpan.FromSeconds(3)); } catch { /* best effort */ }

        MessageBox.Show(
            Strings.Format("Crash_Message", BrandingInfo.AppName),
            Strings.Format("Crash_Title", BrandingInfo.AppName),
            MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(1);
    }

    /// <summary>
    /// Wait (off the UI thread) for a second launch's signal and bring the
    /// main window forward. Part of the single-instance block above.
    /// </summary>
    private void ActivateOnSignal()
    {
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Activate");
        ThreadPool.RegisterWaitForSingleObject(activate, (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is not { } w) return;
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Show();
            w.Activate();
            // Windows only lets a background process steal focus reluctantly;
            // a Topmost flick is the reliable way to surface the window.
            w.Topmost = true;
            w.Topmost = w.DataContext is MainViewModel { AlwaysOnTop: true };
        }), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        Log.Information("{App} shutting down", BrandingInfo.AppName);
        Log.CloseAndFlush();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    // ── Service registration ─────────────────────────────────────────────

    private static void RegisterServices(IServiceCollection services, SettingsService earlySettings)
    {
        // Infrastructure
        services.AddSingleton<AppLogger>();
        services.AddSingleton(earlySettings);
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(5) });
        services.AddSingleton<TelemetryService>();

        // Hamlib / rig control
        services.AddSingleton<HamlibLocatorService>();
        services.AddSingleton<RigctlCommandBuilder>();
        services.AddTransient<HamlibRunnerService>();

        // Domain
        services.AddSingleton<ComPortService>();
        services.AddSingleton<RadioPresetsService>();
        services.AddSingleton<DiagnosisEngine>();
        services.AddTransient<TestRunnerService>();
        services.AddTransient<EnvironmentCheckService>();
        services.AddSingleton<DiscoveryDataService>();
        services.AddTransient<DiscoveryEngine>();
        services.AddTransient<ConfigClueService>();
        services.AddTransient<HandoffBuilder>();
        services.AddSingleton<UpdateCheckService>();
        services.AddSingleton<NativeCheckService>();
        services.AddTransient<LogExportService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ConnectionViewModel>();
        services.AddTransient<TestResultsViewModel>();
        services.AddTransient<RawConsoleViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

}
