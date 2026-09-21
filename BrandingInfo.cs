namespace RigCheck;

/// <summary>
/// All RigCheck / ShackDesk identity constants in one place.
/// Mirror this pattern in every ShackDesk suite app.
/// When ShackDesk.Core NuGet exists, suite-level constants
/// will move there; app-level constants stay here.
/// </summary>
public static class BrandingInfo
{
    // ── App identity ────────────────────────────────────────────
    public const string AppName    = "RigCheck";
    public const string SuiteName  = "ShackDesk";
    public const string FullName   = "RigCheck by ShackDesk";
    public const string Version    = "0.7.1";
    public const string Tagline    = "Know your rig is ready";

    // ── Developer / publisher ────────────────────────────────────
    public const string Developer  = "Mark McDow N4TEK";
    public const string Company    = "My Computer Guru LLC";
    public const string Callsign   = "N4TEK";
    public const string Copyright  = "© 2025–2026 My Computer Guru LLC";

    // ── URLs ─────────────────────────────────────────────────────
    public const string SuiteUrl    = "https://shackdesk.com";
    public const string AppUrl      = "https://shackdesk.com/rigcheck/";
    public const string HelpUrl     = "https://shackdesk.com/faq/#rigcheck";
    public const string GitHubOrg   = "https://github.com/Computer-Tsu";
    public const string GitHubRepo  = "https://github.com/Computer-Tsu/ShackDesk-RigCheck";
    public const string ReleasesUrl = "https://github.com/Computer-Tsu/ShackDesk-RigCheck/releases";
    public const string IssueUrl    = "https://github.com/Computer-Tsu/ShackDesk-RigCheck/issues";
    public const string PrivacyUrl  = "https://shackdesk.com/privacy/";

    // ── Telemetry ─────────────────────────────────────────────────
    // Shared ShackDesk endpoint; see ShackDesk-Backend for the schema.
    public const string TelemetryEndpoint = "https://telemetry.shackdesk.com/report";

    // ── Build channels and expiry ─────────────────────────────────
    // Alpha builds stop running after AlphaExpiryDays so testers stay on
    // current builds and end users are steered away from alphas. Beta
    // builds warn but keep working. Stable builds never expire.
    public const string ChannelAlpha     = "alpha";
    public const string ChannelBeta      = "beta";
    public const string ChannelStable    = "stable";
    public const int    AlphaExpiryDays  = 30;
    public const int    BetaExpiryDays   = 90;
    public const int    ExpiryWarnDays   = 9;   // warn from day 21 of a 30-day alpha

    // ── Hamlib / rigctl resources ─────────────────────────────────
    public const string HamlibDownloadUrl  = "https://github.com/Hamlib/Hamlib/releases";

    /// <summary>
    /// winget command that installs WSJT-X, which bundles Hamlib. Shown to the
    /// operator as a copyable line; RigCheck never runs it. Package ID
    /// verified 2026-09-21 (installs WSJT-X 3.0.2).
    /// </summary>
    public const string WsjtxWingetCommand = "winget install JoeTaylor.WSJT-x";
    public const string WsjtxDownloadUrl   = "https://wsjt.sourceforge.io/wsjtx.html";
    public const string HamlibDocsUrl      = "https://hamlib.github.io";

    // ── Defaults ──────────────────────────────────────────────────
    // Serial settings default to "let Hamlib use the radio model's own
    // defaults" — most operators don't know these values, and the model
    // database usually does. 0 / RadioDefault means the flag is omitted
    // from the rigctl command.
    public const string DefaultRigctldHost = "localhost";
    public const int    DefaultRigctldPort = 4532;
    public const int    DefaultBaudRate    = 0;
    public const string RadioDefault       = "Default";
    public const string DefaultDataBits    = RadioDefault;
    public const string DefaultParity      = RadioDefault;
    public const string DefaultStopBits    = RadioDefault;
    public const string DefaultFlowCtrl    = RadioDefault;
    public const string DefaultPttMethod   = RadioDefault;

    // ── Window / layout ───────────────────────────────────────────
    public const double DefaultWindowWidth  = 720;
    public const double DefaultWindowHeight = 640;
    public const double MinWindowWidth      = 560;
    public const double MinWindowHeight     = 480;

    // ── Logging ───────────────────────────────────────────────────
    public const string LogFileName = "rigcheck-.log";
    public const string LogFolder   = "Logs";

    // ── Settings ──────────────────────────────────────────────────
    public const string SettingsFileName = "rigcheck-settings.json";

    // ── License ───────────────────────────────────────────────────
    public const string License    = "GPL v3";
    public const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";
}
