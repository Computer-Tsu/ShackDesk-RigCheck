using RigCheck.Localization;
using RigCheck.Services;
using System.Text;
using System.Windows;

namespace RigCheck.Views;

/// <summary>Shows every telemetry report stored locally, newest first.</summary>
public partial class TelemetryDataViewer : Window
{
    private readonly TelemetryService _telemetry;

    public TelemetryDataViewer(TelemetryService telemetry)
    {
        _telemetry = telemetry;
        InitializeComponent();
        IntroText.Text = Strings.Format("DataViewer_Intro", BrandingInfo.AppName);
        Load();
    }

    private void Load()
    {
        var reports = _telemetry.GetLocalReports();
        if (reports.Count == 0)
        {
            ReportsText.Text = Strings.Get("DataViewer_Empty");
            return;
        }

        var sb = new StringBuilder();
        foreach (var (name, json) in reports)
        {
            sb.AppendLine($"── {name} ──");
            sb.AppendLine(json);
            sb.AppendLine();
        }
        ReportsText.Text = sb.ToString();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _telemetry.ClearLocalReports();
        Load();
    }
}
