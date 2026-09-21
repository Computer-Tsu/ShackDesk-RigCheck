using RigCheck.Localization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RigCheck.Views;

/// <summary>
/// About box. Text with placeholders is composed here rather than bound,
/// because the values come from constants and never change at runtime.
/// </summary>
public partial class AboutDialog : Window
{
    private readonly string _installId;

    public AboutDialog(string installId)
    {
        _installId = installId;
        InitializeComponent();

        SupportIdText.Text = Strings.Format("About_SupportId", installId);

        Title              = Strings.Format("About_Title", BrandingInfo.AppName);
        VersionText.Text   = Strings.Format("About_Version", BuildInfo.VersionLabel);
        ChannelText.Text   = Strings.Format("About_Channel", BuildInfo.Channel,
                                            BuildInfo.BuildDate?.ToString("yyyy-MM-dd") ?? "?");
        DeveloperText.Text = Strings.Format("About_DevelopedBy", BrandingInfo.Developer);
        LicenseText.Text   = Strings.Format("About_License", BrandingInfo.License);

        if (BuildInfo.ExpiryDate is { } exp)
        {
            ExpiryText.Text       = Strings.Format("About_Expires", BuildInfo.Channel, exp.ToString("yyyy-MM-dd"));
            ExpiryText.Visibility = Visibility.Visible;
        }
    }

    private void CopyId_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(_installId);

    // Each link button carries its URL in Tag so one handler serves them all.
    private void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } && !string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
