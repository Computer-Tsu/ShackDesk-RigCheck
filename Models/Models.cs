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
    OpenConnection,
    GetFrequency,
    GetMode,
    GetPtt,
    GetSmeter,
    GetVfo,
    SetFrequency,
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
    TestId          Id,
    TestStatus      Status,
    string          Message,
    string          DisplayCommand,
    DiagnosticResult? Diagnosis = null)
{
    public static TestResult Pass(TestId id, string message, string displayCommand) =>
        new(id, TestStatus.Pass, message, displayCommand);

    public static TestResult Fail(TestId id, string message, string displayCommand,
                                   DiagnosticResult diagnosis) =>
        new(id, TestStatus.Fail, message, displayCommand, diagnosis);

    public static TestResult Warning(TestId id, string message, string displayCommand) =>
        new(id, TestStatus.Warning, message, displayCommand);

    public static TestResult Skipped(TestId id, string reason) =>
        new(id, TestStatus.Skipped, reason, string.Empty);

    public static TestResult Pending(TestId id) =>
        new(id, TestStatus.Pending, string.Empty, string.Empty);

    public string FriendlyName => Id switch
    {
        TestId.OpenConnection => "Open connection",
        TestId.GetFrequency   => "Get frequency",
        TestId.GetMode        => "Get mode",
        TestId.GetPtt         => "Get PTT state",
        TestId.GetSmeter      => "Get signal meter",
        TestId.GetVfo         => "Get VFO",
        TestId.SetFrequency   => "Set and verify frequency",
        _                     => Id.ToString(),
    };

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

// ── Diagnostic result (referenced by TestResult) ──────────────────────────
// Defined in DiagnosisEngine.cs — declared here for the record type reference
// to avoid circular namespace issues. The concrete record is in Services.
// Re-exported here so Models namespace is self-contained for consumers.

// NOTE: DiagnosticResult is defined in RigCheck.Services to keep service
// logic together. TestResult holds a nullable reference to it.
// No re-export needed — consumers reference RigCheck.Services directly.
