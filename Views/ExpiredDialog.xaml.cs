using RigCheck.Localization;
using System.Diagnostics;
using System.Windows;

namespace RigCheck.Views;

/// <summary>Blocking notice for an expired alpha build.</summary>
public partial class ExpiredDialog : Window
{
    public ExpiredDialog()
    {
        InitializeComponent();

        Title = Strings.Format("Expired_Title", BrandingInfo.AppName);
        MessageText.Text = Strings.Format("Expired_Message",
            BrandingInfo.AppName,
            BuildInfo.ExpiryDate?.ToString("yyyy-MM-dd") ?? "?",
            BrandingInfo.AlphaExpiryDays);
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = BrandingInfo.ReleasesUrl, UseShellExecute = true });
        Close();
    }
}
