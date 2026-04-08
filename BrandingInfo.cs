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
    public const string Version    = "0.5.0-beta";
    public const string Tagline    = "Know your rig is ready";

    // ── Developer / publisher ────────────────────────────────────
    public const string Developer  = "Mark McDow N4TEK";
    public const string Company    = "My Computer Guru LLC";
    public const string Callsign   = "N4TEK";

    // ── URLs ─────────────────────────────────────────────────────
    public const string SuiteUrl   = "https://shackdesk.app";
    public const string AppUrl     = "https://shackdesk.app/rigcheck";
    public const string GitHubOrg  = "https://github.com/Computer-Tsu";
    public const string GitHubRepo = "https://github.com/Computer-Tsu/shackdesk-rigcheck";
    public const string IssueUrl   = "https://github.com/Computer-Tsu/shackdesk-rigcheck/issues";

    // ── Hamlib / rigctl resources ─────────────────────────────────
    public const string HamlibDownloadUrl  = "https://github.com/Hamlib/Hamlib/releases";
    public const string WsjtxDownloadUrl   = "https://wsjt.sourceforge.io/wsjtx.html";
    public const string HamlibDocsUrl      = "https://hamlib.github.io";

    // ── Defaults ──────────────────────────────────────────────────
    public const string DefaultRigctldHost = "localhost";
    public const int    DefaultRigctldPort = 4532;
    public const int    DefaultBaudRate    = 9600;
    public const string DefaultDataBits    = "8";
    public const string DefaultParity      = "None";
    public const string DefaultStopBits    = "1";
    public const string DefaultFlowCtrl    = "None";
    public const string DefaultPttMethod   = "CAT";

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
    public const string License        = "GPL v3";
    public const string LicenseUrl     = "https://www.gnu.org/licenses/gpl-3.0.html";
    public const string CommercialTier = "Commercial license available — contact shackdesk.app";
}
