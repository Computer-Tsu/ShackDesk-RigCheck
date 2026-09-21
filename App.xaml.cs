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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureSerilog();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(RegisterServices)
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

        // Ask once about anonymous diagnostics before anything is sent.
        if (!settings.Current.TelemetryPrompted)
            new FirstRunDialog(settings).ShowDialog();

        DispatcherUnhandledException += (_, e) => OnUnhandledException(e, telemetry);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

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

    protected override async void OnExit(ExitEventArgs e)
    {
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

    private static void RegisterServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<AppLogger>();
        services.AddSingleton<SettingsService>();
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
        services.AddTransient<LogExportService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ConnectionViewModel>();
        services.AddTransient<TestResultsViewModel>();
        services.AddTransient<RawConsoleViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    // ── Logging setup ────────────────────────────────────────────────────

    private static void ConfigureSerilog()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            BrandingInfo.SuiteName,
            BrandingInfo.AppName,
            BrandingInfo.LogFolder);

        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: Path.Combine(logDir, BrandingInfo.LogFileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();
    }
}
