using RigCheck.Models;

namespace RigCheck.Services;

/// <summary>
/// Maps Hamlib failure codes to plain-English diagnostic messages
/// written for non-technical ham radio operators.
///
/// Every message follows the pattern:
///   What happened (one sentence)
///   Check: bulleted list of likely causes in order of probability
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
        Summary: "Hamlib (rigctl.exe) is not installed or could not be found.",
        Checks:
        [
            "Install WSJT-X — it includes Hamlib automatically.",
            "Or download Hamlib for Windows from hamlib.github.io.",
            "After installing, restart RigCheck so it can find the new files.",
        ],
        FixCommand: null,
        LearnMoreUrl: BrandingInfo.HamlibDownloadUrl);

    private static DiagnosticResult PortInUse(string port) => new(
        Summary: $"{port} is already open by another program.",
        Checks:
        [
            "Is WSJT-X, Fldigi, JS8Call, or Winlink currently running?",
            "Close the other program and try again.",
            "Some programs hold the port open even when not actively transmitting.",
            "In WSJT-X: File → Settings → Radio — disconnect rig control before testing here.",
        ],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult PortNotFound(string port) => new(
        Summary: $"{port} was not found on this computer.",
        Checks:
        [
            "Is the USB-to-serial cable plugged in?",
            "Check Device Manager to see which COM port Windows assigned.",
            $"The port may have changed — try a different COM port number.",
            "Some radios need a driver installed (e.g., Icom IC-7300 uses Silicon Labs CP210x).",
        ],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult Timeout(ConnectionConfig cfg) => new(
        Summary: "The radio did not respond within the time limit.",
        Checks:
        [
            "Is the radio powered on?",
            $"Is the baud rate correct? Your radio manual lists the CAT baud rate. Currently set to {cfg.BaudRate}.",
            "Is the CAT / CI-V / RS-232 cable connected to the correct port on the radio?",
            "Does your radio need CAT control enabled in its menu? Check the manual for \"CAT\", \"CI-V\", or \"RS-232\" settings.",
            "Some radios use a different data bits / parity / stop bits setting. Check the radio manual.",
        ],
        FixCommand:  null,
        LearnMoreUrl: null,
        HelpTopics:  HelpContent.TopicsForError(RigctlError.Timeout));

    private static DiagnosticResult NoResponse(ConnectionConfig cfg) => new(
        Summary: "Connected to the port, but the radio returned no data.",
        Checks:
        [
            $"Verify the selected radio model matches your actual radio. Currently: {cfg.RadioModelName}.",
            "Try a lower baud rate — many radios default to 9600 baud.",
            "Check the radio's CAT/CI-V baud rate setting matches RigCheck.",
            "Is a straight-through or null-modem cable needed? Some radios need one vs. the other.",
        ],
        FixCommand:  null,
        LearnMoreUrl: null,
        HelpTopics:  HelpContent.TopicsForError(RigctlError.NoResponse));

    private static DiagnosticResult WrongModel(ConnectionConfig cfg) => new(
        Summary: "The radio responded, but the data didn't match the expected radio model.",
        Checks:
        [
            $"The selected model is \"{cfg.RadioModelName}\" — does this match your actual radio?",
            "Search for your exact model number in the radio selector.",
            "Some radios have multiple Hamlib entries — try nearby entries in the list.",
            "For Icom radios, verify the CI-V address in both the radio menu and RigCheck.",
        ],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult RigctldNotRunning(ConnectionConfig cfg) => new(
        Summary: $"Could not connect to rigctld at {cfg.RigctldHost}:{cfg.RigctldPort}.",
        Checks:
        [
            "Is rigctld running? It must be started separately before RigCheck can connect.",
            $"Verify the host is correct — currently set to \"{cfg.RigctldHost}\".",
            $"Verify the port is correct — currently set to {cfg.RigctldPort} (default is 4532).",
            "Check your firewall is not blocking rigctld.",
        ],
        FixCommand: $"rigctld -m {cfg.ModelId} -r {cfg.ComPort} -s {cfg.BaudRate} -t {cfg.RigctldPort}",
        LearnMoreUrl: null);

    private static DiagnosticResult Cancelled() => new(
        Summary: "The operation was cancelled.",
        Checks: [],
        FixCommand: null,
        LearnMoreUrl: null);

    private static DiagnosticResult Unknown(string rawMessage) => new(
        Summary: "An unexpected error occurred.",
        Checks:
        [
            "Check the raw error message below for clues.",
            "Try running the shown command manually in a CMD window to see the full output.",
            "Export the log and ask for help in your club or post to a support forum.",
        ],
        FixCommand: null,
        LearnMoreUrl: BrandingInfo.IssueUrl,
        RawError: rawMessage);
}

// ── Result type ───────────────────────────────────────────────────────────

public record DiagnosticResult(
    string      Summary,
    string[]    Checks,
    string?     FixCommand,
    string?     LearnMoreUrl,
    string?     RawError    = null,
    HelpTopic[] HelpTopics  = null!)
{
    // Ensure HelpTopics is never null
    public HelpTopic[] HelpTopics { get; init; } = HelpTopics ?? [];

    public static readonly DiagnosticResult Ok = new(
        Summary:    string.Empty,
        Checks:     [],
        FixCommand: null,
        LearnMoreUrl: null);

    public bool HasChecks     => Checks.Length > 0;
    public bool HasFixCommand => FixCommand is not null;
    public bool HasHelpTopics => HelpTopics.Length > 0;
    public bool IsOk          => string.IsNullOrEmpty(Summary);
}
