using RigCheck.Localization;
using RigCheck.Models;
using Serilog;
using System.IO;
using System.Text;

namespace RigCheck.Services;

/// <summary>
/// Exports test results to a plain-text file the operator can email
/// to a club Elmer or post to a support forum.
///
/// Format: human-readable, no JSON, no markup.
/// Includes all connection settings, each test result, and any
/// diagnostic messages so the recipient has full context.
/// </summary>
public class LogExportService
{
    /// <summary>
    /// Write a test suite result to a file chosen by the user.
    /// Returns the path written, or null on failure.
    /// </summary>
    public async Task<string?> ExportAsync(TestSuiteResult suite, string outputPath)
    {
        try
        {
            var text = BuildReport(suite);
            await File.WriteAllTextAsync(outputPath, text, Encoding.UTF8);
            Log.Information("Log exported to {Path}", outputPath);
            return outputPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export log to {Path}", outputPath);
            return null;
        }
    }

    /// <summary>
    /// Build the report as a string (also used for clipboard copy).
    /// </summary>
    public string BuildReport(TestSuiteResult suite)
    {
        var sb = new StringBuilder();
        var cfg = suite.Config;
        var now = DateTime.Now;
        var radioDefault = Strings.Get("Export_RadioDefault");

        // Labels are padded to a fixed width so translated labels still line up
        static string Row(string label, object? value) => $"  {label + ":",-14}{value}";

        // ── Header ────────────────────────────────────────────────────────
        sb.AppendLine("==========================================================");
        sb.AppendLine($"  {BrandingInfo.FullName}  {BuildInfo.VersionLabel}");
        sb.AppendLine($"  {BrandingInfo.Tagline}");
        sb.AppendLine("==========================================================");
        sb.AppendLine();
        sb.AppendLine(Row(Strings.Get("Export_ReportGenerated"), now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine(Row(Strings.Get("Export_Computer"), Environment.MachineName));
        sb.AppendLine();

        // ── Connection settings ───────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine(Strings.Get("Export_ConnectionSettings"));
        sb.AppendLine("----------------------------------------------------------");

        if (cfg.UseRigctld)
        {
            sb.AppendLine(Row(Strings.Get("Export_Mode"), Strings.Get("Export_ModeRigctld")));
            sb.AppendLine(Row(Strings.Get("Export_Host"), cfg.RigctldHost));
            sb.AppendLine(Row(Strings.Get("Export_Port"), cfg.RigctldPort));
        }
        else
        {
            sb.AppendLine(Row(Strings.Get("Export_Mode"), Strings.Get("Export_ModeSerial")));
            sb.AppendLine(Row(Strings.Get("Export_ComPort"), cfg.ComPort));
            sb.AppendLine(Row(Strings.Get("Export_BaudRate"), cfg.BaudRate > 0 ? cfg.BaudRate.ToString() : radioDefault));
            sb.AppendLine(Row(Strings.Get("Export_DataBits"), cfg.DataBits > 0 ? cfg.DataBits.ToString() : radioDefault));
            sb.AppendLine(Row(Strings.Get("Export_Parity"), cfg.Parity));
            sb.AppendLine(Row(Strings.Get("Export_StopBits"), cfg.StopBits));
            sb.AppendLine(Row(Strings.Get("Export_FlowCtrl"), cfg.FlowControl));
            sb.AppendLine(Row(Strings.Get("Export_PttMethod"), cfg.PttMethod));
        }

        sb.AppendLine(Row(Strings.Get("Export_Radio"), Strings.Format("Export_HamlibId", cfg.RadioModelName, cfg.ModelId)));
        sb.AppendLine();

        // ── Test results ──────────────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine(Strings.Get("Export_TestResults"));
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine();

        foreach (var result in suite.Results)
        {
            var icon = result.Status switch
            {
                TestStatus.Pass    => "[PASS]",
                TestStatus.Fail    => "[FAIL]",
                TestStatus.Warning => "[WARN]",
                TestStatus.Skipped => "[SKIP]",
                _                  => "[    ]",
            };

            sb.AppendLine($"  {icon}  {result.FriendlyName}");
            if (!string.IsNullOrEmpty(result.Message))
                sb.AppendLine($"         {result.Message}");

            // Show the rigctl command so the reader can reproduce it
            if (!string.IsNullOrEmpty(result.DisplayCommand))
                sb.AppendLine($"         {Strings.Format("Export_Command", result.DisplayCommand)}");

            // Show diagnostic detail on failures
            if (result.HasDiagnosis && result.Diagnosis is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"         {Strings.Format("Export_Diagnosis", result.Diagnosis.Summary)}");
                foreach (var check in result.Diagnosis.Checks)
                    sb.AppendLine($"           • {check}");
                if (result.Diagnosis.HasFixCommand)
                    sb.AppendLine($"           {Strings.Format("Export_Try", result.Diagnosis.FixCommand!)}");
            }

            sb.AppendLine();
        }

        // ── Summary ───────────────────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine(Strings.Get("Export_Summary"));
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine(Row(Strings.Get("Export_Passed"), suite.PassCount));
        sb.AppendLine(Row(Strings.Get("Export_Failed"), suite.FailCount));
        sb.AppendLine(Row(Strings.Get("Export_Warnings"), suite.WarningCount));
        sb.AppendLine();

        sb.AppendLine($"  {Strings.Get(suite.AllPassed ? "Export_AllPassed" : "Export_SomeFailed")}");
        sb.AppendLine();

        // ── Footer ────────────────────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine($"  {BrandingInfo.AppUrl}");
        sb.AppendLine($"  {Strings.Format("Export_Issues", BrandingInfo.IssueUrl)}");
        sb.AppendLine("----------------------------------------------------------");

        return sb.ToString();
    }
}
