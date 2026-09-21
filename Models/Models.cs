namespace RigCheck.Models;

// ── Connection configuration ──────────────────────────────────────────────

/// <summary>
/// All parameters needed to connect to a radio via Hamlib.
/// Passed to RigctlCommandBuilder and diagnostic services.
/// </summary>
public record ConnectionConfig
{
    // Serial
    public int    ModelId        { get; init; } = 1;
    public string RadioModelName { get; init; } = string.Empty;
    public string ComPort        { get; init; } = string.Empty;
    public int    BaudRate       { get; init; } = 9600;
    public int    DataBits       { get; init; } = 8;
    public string Parity         { get; init; } = "None";
    public string StopBits       { get; init; } = "1";
    public string FlowControl    { get; init; } = "None";
    public string PttMethod      { get; init; } = "CAT";

    // Network
    public bool   UseRigctld    { get; init; } = false;
    public string RigctldHost   { get; init; } = "localhost";
    public int    RigctldPort   { get; init; } = 4532;
}

// ── Test result ───────────────────────────────────────────────────────────

public enum TestId
{
    // Connection tests — need a radio
    OpenConnection,
    GetFrequency,
    GetMode,
    GetPtt,
    GetSmeter,
    GetVfo,
    SetFrequency,

    // Environment checks — user-initiated Scan, no radio needed
    EnvWindows,
    EnvHamlib,
    EnvRigctlVersion,
    EnvRigctlPath,
    EnvRadioModel,
    EnvSerialDrivers,
    EnvRigctld,
    EnvAutostart,
    EnvFirewall,
    EnvRadioApps,
}

public enum TestStatus
{
    Pass,
    Fail,
    Warning,
    Skipped,
    Running,
    Pending,
}

/// <summary>
/// Result of a single diagnostic test.
/// DisplayCommand is the rigctl command the user can copy and run themselves.
/// </summary>
public record TestResult(
    TestId            Id,
    TestStatus        Status,
    string            Message,
    string            DisplayCommand,
    DiagnosticResult? Diagnosis = null,
    Services.RigctlError? Error = null)
{
    public static TestResult Pass(TestId id, string message, string displayCommand) =>
        new(id, TestStatus.Pass, message, displayCommand);

    public static TestResult Fail(TestId id, string message, string displayCommand,
                                   DiagnosticResult diagnosis, Services.RigctlError? error = null) =>
        new(id, TestStatus.Fail, message, displayCommand, diagnosis, error);

    public static TestResult Warning(TestId id, string message, string displayCommand) =>
        new(id, TestStatus.Warning, message, displayCommand);

    public static TestResult Skipped(TestId id, string reason) =>
        new(id, TestStatus.Skipped, reason, string.Empty);

    public static TestResult Pending(TestId id) =>
        new(id, TestStatus.Pending, string.Empty, string.Empty);

    // Test names are resource keys "Test_{TestId}" so the list is translatable.
    public string FriendlyName =>
        Localization.Strings.TryGet($"Test_{Id}", out var name) ? name : Id.ToString();

    public bool HasDiagnosis => Diagnosis is not null && !Diagnosis.IsOk;
}

/// <summary>
/// Complete result of running the full test suite.
/// </summary>
public record TestSuiteResult(
    List<TestResult> Results,
    ConnectionConfig Config)
{
    public int PassCount    => Results.Count(r => r.Status == TestStatus.Pass);
    public int FailCount    => Results.Count(r => r.Status == TestStatus.Fail);
    public int WarningCount => Results.Count(r => r.Status == TestStatus.Warning);

    public bool AllPassed => FailCount == 0;
    public bool AnyFailed => FailCount > 0;
}

// ── Radio presets ─────────────────────────────────────────────────────────

/// <summary>
/// A quick-start preset for a popular radio model.
/// Selecting a preset fills all connection configuration fields.
/// </summary>
public record RadioPreset
{
    public string Name         { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public int    HamlibModelId { get; init; }
    public int    BaudRate     { get; init; } = 9600;
    public string DataBits     { get; init; } = "8";
    public string Parity       { get; init; } = "None";
    public string StopBits     { get; init; } = "1";
    public string FlowControl  { get; init; } = "None";
    public string PttMethod    { get; init; } = "CAT";
    public string Notes        { get; init; } = string.Empty;  // e.g. "CI-V address default 94"
    public int    Popularity   { get; init; } = 0;             // higher sorts first in the list
}

// ── COM port info ─────────────────────────────────────────────────────────

/// <summary>
/// Describes a COM port available on the system, with optional USB VID/PID
/// identification and a radio family hint where the cable is recognizable.
/// </summary>
public record ComPortInfo(
    string   PortName,
    string   FriendlyName,
    string   DeviceType,        // "USB-Serial", "Bluetooth", "Physical", "Unknown"
    bool     IsUsbSerial,
    string?  Vid,               // USB Vendor ID (4 hex chars), null if not USB
    string?  Pid,               // USB Product ID (4 hex chars), null if not USB
    string?  CableHint,         // e.g. "Icom CI-V USB cable (IC-7300, IC-705, IC-7610)"
    string?  RadioFamily,       // e.g. "Icom", "Yaesu", null if generic/unknown
    string[] SuggestedPresets)  // preset names from RadioPresetsService, may be empty
{
    /// <summary>Label shown in the COM port dropdown.</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(FriendlyName) || FriendlyName == PortName
            ? PortName
            : $"{PortName} — {FriendlyName}";

    /// <summary>
    /// True if this port's cable was recognized as a known radio interface.
    /// When true, CableHint and SuggestedPresets are populated.
    /// </summary>
    public bool HasRadioHint => RadioFamily is not null;
}

// ── Diagnosis ─────────────────────────────────────────────────────────────

/// <summary>
/// A short help article surfaced alongside a failed test.
/// </summary>
public record HelpTopic(string Title, string Body);

/// <summary>
/// Plain-English explanation of a failed test: what happened, what to check,
/// and an optional command or link that may fix it.
/// </summary>
public record DiagnosticResult(
    string      Summary,
    string[]    Checks,
    string?     FixCommand,
    string?     LearnMoreUrl,
    string?     RawError    = null,
    HelpTopic[] HelpTopics  = null!)
{
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

// ── Results transcript ────────────────────────────────────────────────────
// Free-form lines that sit between test results in the results panel: the
// commands and raw serial exchanges of a discovery run, so a technical user
// can replicate them with a terminal program.

public enum TranscriptKind
{
    /// <summary>A rigctl / Hamlib command line as it would be typed.</summary>
    Command,
    /// <summary>Bytes written to the serial port (hex, with ASCII where printable).</summary>
    SerialTx,
    /// <summary>Bytes read back from the serial port.</summary>
    SerialRx,
    /// <summary>A reply from rigctl or the radio, already decoded.</summary>
    Response,
    /// <summary>Progress line while probing ("Trying COM3 at 9600…").</summary>
    Trying,
    /// <summary>Something found — a rig, a running rigctld.</summary>
    Found,
    /// <summary>Anything else worth showing in muted text.</summary>
    Note,
}

public record TranscriptLine(TranscriptKind Kind, string Text);

// ── Discovery (Find my radio) ─────────────────────────────────────────────
// Declarative rig knowledge loaded from rig_families.json, rig_ids.json,
// and port_skip_patterns.json. The data can only name a probe operation
// from RigCheck's fixed set; it never carries command bytes.

/// <summary>A protocol family and how to probe for it.</summary>
public record RigFamily
{
    public string Id              { get; init; } = string.Empty;
    public string Name            { get; init; } = string.Empty;
    public string Probe           { get; init; } = string.Empty;   // ProbeOperation name
    public string? FallbackProbe  { get; init; }                   // tried when Probe gets no reply
    public int[]  Bauds           { get; init; } = [];
    public int[]  HamlibBackends  { get; init; } = [];             // thousands digit of model numbers
    public string[] VendorHints   { get; init; } = [];             // matched against cable RadioFamily
    public int    HandoffStopBits { get; init; } = 1;
    public int    DefaultModelId  { get; init; }                   // when the rig confirms the family but not itself
    public double Weight          { get; init; } = 0.5;
}

/// <summary>How a rig names itself, mapped to a Hamlib model.</summary>
public record RigIdEntry
{
    public string Family        { get; init; } = string.Empty;
    public string Reply         { get; init; } = string.Empty;    // "ID023;" or CI-V address "94"
    public string Name          { get; init; } = string.Empty;
    public int    HamlibModelId { get; init; }
}

/// <summary>A port-name pattern discovery must never open.</summary>
public record PortSkipPattern
{
    public string Pattern     { get; init; } = string.Empty;
    public string DeviceClass { get; init; } = string.Empty;   // rotator, amplifier, antenna, gps, bluetooth
}

/// <summary>A working connection discovery found and verified.</summary>
public record DiscoveredRig(
    string  Port,             // "COM3", or "localhost:4532" for rigctld
    int     Baud,             // 0 for network transports
    string  FamilyId,
    int     HamlibModelId,
    string  ModelName,
    double  Score,            // 1.0 = rig named itself, 0.8 = frequency reply in a ham band
    int     HandoffStopBits,
    bool    UseRigctld = false);

public enum ProbeEventKind { Trying, Sent, Received, Skipped, InUse, Found, PortDone, Note }

/// <summary>One step of a discovery run, emitted as it happens.</summary>
public record ProbeEvent(
    ProbeEventKind Kind,
    string         Port,
    string         Message,
    string?        Bytes    = null,      // hex dump of what was sent or received
    DiscoveredRig? Rig      = null);
