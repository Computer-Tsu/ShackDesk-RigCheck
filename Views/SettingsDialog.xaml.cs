using RigCheck.Localization;
using RigCheck.Logging;
using RigCheck.Services;
using RigCheck.ViewModels;
using System.Diagnostics;
using System.Windows;

namespace RigCheck.Views;

/// <summary>
/// Settings window. Diagnostics and logging choices are pending until OK;
/// the file and ID actions happen immediately after confirmation.
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsService  _settings;
    private readonly TelemetryService _telemetry;

    public SettingsDialog(SettingsService settings, TelemetryService telemetry)
    {
        _settings  = settings;
        _telemetry = telemetry;
        InitializeComponent();

        WhatIsSentText.Text = Strings.Format("Telemetry_WhatIsSent", BrandingInfo.AppName);
        LogPathText.Text    = AppLogger.LogDirectory;
        SupportIdText.Text  = _settings.Current.InstallId;

        LogLevelCombo.ItemsSource = new[]
        {
            new Choice(AppLogger.LevelOff,      Strings.Get("LogLevel_Off")),
            new Choice(AppLogger.LevelErrors,   Strings.Get("LogLevel_Error")),
            new Choice(AppLogger.LevelNormal,   Strings.Get("LogLevel_Information")),
            new Choice(AppLogger.LevelDetailed, Strings.Get("LogLevel_Debug")),
        };

        // Load current values into the pending controls
        TelemetryCheckBox.IsChecked = _settings.Current.TelemetryEnabled;
        NotifyFlashCheckBox.IsChecked = _settings.Current.NotifyFlash;
        NotifySoundCheckBox.IsChecked = _settings.Current.NotifySound;
        // Null = never chosen: show the channel default (on for alpha/beta, off for stable)
        UpdateCheckBox.IsChecked = _settings.Current.UpdateCheckEnabled ?? !BuildInfo.IsStable;
        LogLevelCombo.SelectedValue = AppLogger.ResolveLevel(_settings.Current.LogLevel);
    }

    // ── OK / Cancel ───────────────────────────────────────────────────────

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var level = LogLevelCombo.SelectedValue as string ?? AppLogger.ChannelDefaultLevel;

        _settings.Update(s =>
        {
            s.TelemetryEnabled = TelemetryCheckBox.IsChecked == true;
            s.NotifyFlash      = NotifyFlashCheckBox.IsChecked == true;
            s.NotifySound      = NotifySoundCheckBox.IsChecked == true;
            s.UpdateCheckEnabled = UpdateCheckBox.IsChecked == true;
            s.LogLevel         = level;
        });

        if (level != AppLogger.CurrentLevel)
            AppLogger.Configure(level);

        DialogResult = true;
    }

    // ── Immediate actions ─────────────────────────────────────────────────

    private void ViewData_Click(object sender, RoutedEventArgs e) =>
        new TelemetryDataViewer(_telemetry) { Owner = this }.ShowDialog();

    private void Privacy_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo { FileName = BrandingInfo.PrivacyUrl, UseShellExecute = true });

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(AppLogger.LogDirectory);
        Process.Start(new ProcessStartInfo { FileName = AppLogger.LogDirectory, UseShellExecute = true });
    }

    private void DeleteLogs_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this,
            Strings.Format("Settings_DeleteLogsConfirm", BrandingInfo.AppName),
            Strings.Get("Settings_DeleteLogs"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var removed = AppLogger.DeleteLogs();
        LogActionStatus.Text = Strings.Format("Settings_DeleteLogsDone", removed);
    }

    private void CopyId_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(_settings.Current.InstallId);

    // A new ID also removes the local report copies, which carry the old one.
    private void ResetId_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this,
            Strings.Get("Settings_ResetIdConfirm"),
            Strings.Get("Settings_SupportIdHeading"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        _settings.Update(s => s.InstallId = Guid.NewGuid().ToString("D"));
        _telemetry.ClearLocalReports();
        SupportIdText.Text = _settings.Current.InstallId;
    }
}
