using RigCheck.Localization;
using RigCheck.Models;

namespace RigCheck.Services;

/// <summary>
/// Maps Hamlib failure codes to plain-English diagnostic messages
/// written for non-technical ham radio operators.
///
/// Every message follows the pattern:
///   What happened (one sentence)
///   Check: bulleted list of likely causes in order of probability
///
/// All text comes from Strings.resx; each bullet is its own key so
/// translators can work one sentence at a time.
/// </summary>
public class DiagnosisEngine
{
    public DiagnosticResult Diagnose(RigctlResult result, ConnectionConfig cfg)
    {
        if (result.IsSuccess)
            return DiagnosticResult.Ok;

        return result.Error switch
        {
            RigctlError.HamlibNotFound     => HamlibNotFound(),
            RigctlError.PortInUse          => PortInUse(cfg.ComPort),
            RigctlError.PortNotFound       => PortNotFound(cfg.ComPort),
            RigctlError.Timeout            => Timeout(cfg),
            RigctlError.NoResponse         => NoResponse(cfg),
            RigctlError.WrongModel         => WrongModel(cfg),
            RigctlError.RigctldNotRunning  => RigctldNotRunning(cfg),
            RigctlError.Cancelled          => Cancelled(),
            _                              => Unknown(result.ErrorMessage),
        };
    }

    // ── Diagnosis messages ────────────────────────────────────────────────

    private static DiagnosticResult HamlibNotFound() => new(
        Summary: Strings.Get("Diag_HamlibNotFound_Summary"),
        Checks:
        [
            Strings.Get("Diag_HamlibNotFound_1"),
            Strings.Get("Diag_HamlibNotFound_2"),
            Strings.Format("Diag_HamlibNotFound_3", BrandingInfo.AppName),
        ],
        FixCommand: null,
        LearnMoreUrl: BrandingInfo.HamlibDownloadUrl);

    private static DiagnosticResult PortInUse(string port) => new(
        Summary: Strings.Format("Diag_PortInUse_Summary", port),
        Checks:
        [
            Strings.Get("Diag_PortInUse_1"),
            Strings.Get("Diag_PortInUse_2"),
            Strings.Get("Diag_PortInUse_3"),
            Strings.Get("Diag_PortInUse_4"),
        ],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult PortNotFound(string port) => new(
        Summary: Strings.Format("Diag_PortNotFound_Summary", port),
        Checks:
        [
            Strings.Get("Diag_PortNotFound_1"),
            Strings.Get("Diag_PortNotFound_2"),
            Strings.Get("Diag_PortNotFound_3"),
            Strings.Get("Diag_PortNotFound_4"),
        ],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult Timeout(ConnectionConfig cfg) => new(
        Summary: Strings.Get("Diag_Timeout_Summary"),
        Checks:
        [
            Strings.Get("Diag_Timeout_1"),
            Strings.Format("Diag_Timeout_2", cfg.BaudRate > 0 ? cfg.BaudRate.ToString() : Strings.Get("Diag_BaudDefault")),
            Strings.Get("Diag_Timeout_3"),
            Strings.Get("Diag_Timeout_4"),
            Strings.Get("Diag_Timeout_5"),
        ],
        FixCommand:  null,
        LearnMoreUrl: null,
        HelpTopics:  HelpContent.TopicsForError(RigctlError.Timeout));

    private static DiagnosticResult NoResponse(ConnectionConfig cfg) => new(
        Summary: Strings.Get("Diag_NoResponse_Summary"),
        Checks:
        [
            Strings.Format("Diag_NoResponse_1", cfg.RadioModelName),
            Strings.Get("Diag_NoResponse_2"),
            Strings.Format("Diag_NoResponse_3", BrandingInfo.AppName),
            Strings.Get("Diag_NoResponse_4"),
        ],
        FixCommand:  null,
        LearnMoreUrl: null,
        HelpTopics:  HelpContent.TopicsForError(RigctlError.NoResponse));

    private static DiagnosticResult WrongModel(ConnectionConfig cfg) => new(
        Summary: Strings.Get("Diag_WrongModel_Summary"),
        Checks:
        [
            Strings.Format("Diag_WrongModel_1", cfg.RadioModelName),
            Strings.Get("Diag_WrongModel_2"),
            Strings.Get("Diag_WrongModel_3"),
            Strings.Format("Diag_WrongModel_4", BrandingInfo.AppName),
        ],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult RigctldNotRunning(ConnectionConfig cfg) => new(
        Summary: Strings.Format("Diag_Rigctld_Summary", cfg.RigctldHost, cfg.RigctldPort),
        Checks:
        [
            Strings.Format("Diag_Rigctld_1", BrandingInfo.AppName),
            Strings.Format("Diag_Rigctld_2", cfg.RigctldHost),
            Strings.Format("Diag_Rigctld_3", cfg.RigctldPort),
            Strings.Get("Diag_Rigctld_4"),
        ],
        FixCommand: $"rigctld -m {cfg.ModelId} -r {cfg.ComPort} -s {cfg.BaudRate} -t {cfg.RigctldPort}",
        LearnMoreUrl: null);

    private static DiagnosticResult Cancelled() => new(
        Summary: Strings.Get("Diag_Cancelled_Summary"),
        Checks: [],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult Unknown(string rawMessage) => new(
        Summary: Strings.Get("Diag_Unknown_Summary"),
        Checks:
        [
            Strings.Get("Diag_Unknown_1"),
            Strings.Get("Diag_Unknown_2"),
            Strings.Get("Diag_Unknown_3"),
        ],
        FixCommand: null,
        LearnMoreUrl: BrandingInfo.IssueUrl,
        RawError: rawMessage);
}
