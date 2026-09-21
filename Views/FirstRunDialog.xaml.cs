using RigCheck.Localization;
using RigCheck.Services;
using System.Diagnostics;
using System.Windows;

namespace RigCheck.Views;

/// <summary>First-launch choice about anonymous diagnostics. Records that the question was asked.</summary>
public partial class FirstRunDialog : Window
{
    private readonly SettingsService _settings;

    public FirstRunDialog(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();

        Title              = Strings.Format("FirstRun_Title", BrandingInfo.AppName);
        IntroText.Text     = Strings.Format("FirstRun_Intro", BrandingInfo.AppName);
        WhatIsSentText.Text = Strings.Format("Telemetry_WhatIsSent", BrandingInfo.AppName);

        // Test-channel builds are where the data matters most; stable defaults to off.
        EnableCheckBox.IsChecked = BuildInfo.IsAlpha || BuildInfo.IsBeta;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        _settings.Update(s =>
        {
            s.TelemetryEnabled  = EnableCheckBox.IsChecked == true;
            s.TelemetryPrompted = true;
        });
        Close();
    }

    private void Privacy_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo { FileName = BrandingInfo.PrivacyUrl, UseShellExecute = true });
}
