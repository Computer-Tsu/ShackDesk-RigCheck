using RigCheck.Localization;
using RigCheck.Services;
using System.Diagnostics;
using System.Windows;

namespace RigCheck.Views;

/// <summary>Settings window. Changes apply immediately; there is no Save button.</summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsService  _settings;
    private readonly TelemetryService _telemetry;
    private bool _loading = true;

    public SettingsDialog(SettingsService settings, TelemetryService telemetry)
    {
        _settings  = settings;
        _telemetry = telemetry;
        InitializeComponent();

        WhatIsSentText.Text = Strings.Format("Telemetry_WhatIsSent", BrandingInfo.AppName);
        TelemetryCheckBox.IsChecked = _settings.Current.TelemetryEnabled;
        _loading = false;
    }

    private void Telemetry_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Update(s => s.TelemetryEnabled = TelemetryCheckBox.IsChecked == true);
    }

    private void ViewData_Click(object sender, RoutedEventArgs e) =>
        new TelemetryDataViewer(_telemetry) { Owner = this }.ShowDialog();

    private void Privacy_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo { FileName = BrandingInfo.PrivacyUrl, UseShellExecute = true });
}
