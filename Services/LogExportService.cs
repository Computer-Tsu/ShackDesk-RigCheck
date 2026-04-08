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

        // ── Header ────────────────────────────────────────────────────────
        sb.AppendLine("==========================================================");
        sb.AppendLine($"  {BrandingInfo.FullName}  {BrandingInfo.Version}");
        sb.AppendLine($"  {BrandingInfo.Tagline}");
        sb.AppendLine("==========================================================");
        sb.AppendLine();
        sb.AppendLine($"Report generated: {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Computer:         {Environment.MachineName}");
        sb.AppendLine();

        // ── Connection settings ───────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine("CONNECTION SETTINGS");
        sb.AppendLine("----------------------------------------------------------");

        if (cfg.UseRigctld)
        {
            sb.AppendLine($"  Mode:       rigctld network");
            sb.AppendLine($"  Host:       {cfg.RigctldHost}");
            sb.AppendLine($"  Port:       {cfg.RigctldPort}");
        }
        else
        {
            sb.AppendLine($"  Mode:       Direct serial");
            sb.AppendLine($"  COM port:   {cfg.ComPort}");
            sb.AppendLine($"  Baud rate:  {cfg.BaudRate}");
            sb.AppendLine($"  Data bits:  {cfg.DataBits}");
            sb.AppendLine($"  Parity:     {cfg.Parity}");
            sb.AppendLine($"  Stop bits:  {cfg.StopBits}");
            sb.AppendLine($"  Flow ctrl:  {cfg.FlowControl}");
            sb.AppendLine($"  PTT method: {cfg.PttMethod}");
        }

        sb.AppendLine($"  Radio:      {cfg.RadioModelName} (Hamlib ID {cfg.ModelId})");
        sb.AppendLine();

        // ── Test results ──────────────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine("TEST RESULTS");
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
                sb.AppendLine($"         Command: {result.DisplayCommand}");

            // Show diagnostic detail on failures
            if (result.HasDiagnosis && result.Diagnosis is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"         DIAGNOSIS: {result.Diagnosis.Summary}");
                foreach (var check in result.Diagnosis.Checks)
                    sb.AppendLine($"           • {check}");
                if (result.Diagnosis.HasFixCommand)
                    sb.AppendLine($"           Try: {result.Diagnosis.FixCommand}");
            }

            sb.AppendLine();
        }

        // ── Summary ───────────────────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine("SUMMARY");
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine($"  Passed:   {suite.PassCount}");
        sb.AppendLine($"  Failed:   {suite.FailCount}");
        sb.AppendLine($"  Warnings: {suite.WarningCount}");
        sb.AppendLine();

        if (suite.AllPassed)
            sb.AppendLine("  All tests passed. Your rig control connection looks good!");
        else
            sb.AppendLine("  One or more tests failed. See the FAIL entries above for diagnosis.");

        sb.AppendLine();

        // ── Footer ────────────────────────────────────────────────────────
        sb.AppendLine("----------------------------------------------------------");
        sb.AppendLine($"  {BrandingInfo.AppUrl}");
        sb.AppendLine($"  Issues / feedback: {BrandingInfo.IssueUrl}");
        sb.AppendLine("----------------------------------------------------------");

        return sb.ToString();
    }
}
