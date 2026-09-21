# Changelog

All notable changes to RigCheck are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Version numbers: patch (x.x.1) for landed features and fixes, minor (x.1.0) for milestones.

## [0.7.1] - 2026-09-21

### Added
- Beta and stable releases are built from version tags: `v0.7.0-beta.1` publishes
  `RigCheck-0.7.0-beta.1.exe` as a pre-release on the beta channel (90-day reminder), `v0.7.0`
  publishes `RigCheck-0.7.0.exe` as a stable release. The tag must match the project version.
  The version in the title bar shows the tag suffix (`0.7.0-beta.1`)

### Changed
- RigCheck runs as a single instance: starting it again brings the open window to the front.
  Two instances contended for the same COM port and overwrote each other's settings (#10)

## [0.7.0] - 2026-09-21

**Milestone: environment checks and Find my radio, proven on a real Icom IC-7300.**

Scan PC, Find my radio, and Run Tests all did their job on real hardware today. Everything the
bench found — CI-V broadcast never answers, WSJT-X names its Hamlib `rigctl-wsjtx.exe`, the
connection test hung in rigctl's interactive mode, Hamlib says nothing on failure without `-vv`,
`get_vfo` is not a fault on Icoms — is fixed in 0.6.10 through 0.6.17 below. This is the version
the first beta will be cut from.

## [0.6.17] - 2026-09-21

### Fixed
- Pasting a full command line from the results (`rigctl-wsjtx -m 3073 -r COM3 -s 115200 f`) into
  the raw console sent the connection options twice; the exe name and connection options are now
  stripped so only the subcommand runs, and the box shows a placeholder saying so

### Added
- Screenshots from the first IC-7300 run in the README

## [0.6.16] - 2026-09-21

First all-green run on an IC-7300; one false pass corrected.

### Fixed
- "Get VFO" reported a pass with a page of Hamlib trace as the VFO name. The IC-7300 (like most
  Icoms) has no get_vfo in Hamlib; rigctl printed "Feature not available" but still exited 0.
  Hamlib's own error line is now recognised whatever the exit code, and an unsupported query is a
  warning that says so — the radio is fine, the command just does not exist for it
- Result messages show only the first line of rigctl's output, never a trace

## [0.6.15] - 2026-09-21

Failures now say why.

### Fixed
- Hamlib 4.7.1 prints nothing when rigctl fails at its default verbosity, so a port held by
  WSJT-X, a missing rigctld, and a radio that is off all came out as "an unexpected error".
  rigctl now runs with `-vv`, its banner line is stripped from the output, and its actual
  messages ("serial port COM3 is already open", "does not exist", "failed to connect") drive the
  diagnosis
- The "port in use" diagnosis names the program that is running right now (WSJT-X, Fldigi,
  JS8Call, Flrig, rigctld, Winlink Express, VARA) — checked only when that failure occurs
- The suggested rigctld command uses the name that exists on the PC (`rigctld-wsjtx`) and omits
  `-s` when the baud is Radio default
- The Quick preset picker shows the saved radio on startup instead of "Choose your radio…"

## [0.6.14] - 2026-09-21

Run Tests works against a real radio.

### Fixed
- The "Open connection" test ran rigctl with no command, which does not connect-and-exit — it
  enters rigctl's interactive mode and waits for keyboard input until RigCheck's timeout, so
  every run reported "the radio did not respond" and skipped the other six tests, even though
  Hamlib could talk to the radio in 7 ms. The test now reads the frequency, which proves the
  round trip; rigctl's input is also closed at launch so it can never wait on a keyboard again
- WSJT-X stores several settings as Qt variant blobs (`(...PTT_method_VOX...)`); the
  configuration reader now decodes them instead of passing the blob through to the handoff

## [0.6.13] - 2026-09-21

WSJT-X's Hamlib is found.

### Fixed
- Hamlib was reported missing on a PC with WSJT-X installed: WSJT-X ships its copy as
  `rigctl-wsjtx.exe` (and `rigctld-wsjtx.exe`), not `rigctl.exe`. Both names are now tried, the
  install folder is also read from the Uninstall registry key, and either name is accepted on PATH
- Copyable commands start with the name that exists on the PC (`rigctl-wsjtx …` for WSJT-X users)
- Scan PC re-searches for Hamlib, so installing WSJT-X while RigCheck is open updates the banner
  and enables Run Tests without a restart

## [0.6.12] - 2026-09-21

### Added
- When Scan PC finds no Hamlib, the diagnosis shows the one-line winget command that installs
  WSJT-X (`winget install JoeTaylor.WSJT-x`) as a copyable fix — RigCheck never runs it

### Fixed
- Summary line reads "115200 baud, 8N1 (8 data bits, no parity, 1 stop)" instead of "1 stop bit(s)"
- Text copied from the results panel no longer carries a trailing space where a Copy button sat

## [0.6.11] - 2026-09-21

### Added
- Find my radio ends with the sentence the operator came for — "Your radio: Icom IC-7300 on COM3
  — 115200 baud, 8 data bits, no parity, 1 stop bit" — in the results and in the status bar

## [0.6.10] - 2026-09-21

First lessons from a real IC-7300.

### Fixed
- Find my radio never got an answer from an Icom: the read-ID was sent to CI-V address 00
  (broadcast), which rigs act on but by design never answer. When only our own echo comes
  back (Echo Back is on by default on the IC-7300) or nothing at all, RigCheck now addresses
  each CI-V address in `rig_ids.json` in turn, the selected radio's first
- Our own frame echoed back is now reported as what it is — a CI-V radio listening at that
  speed — instead of "nothing recognisable"
- Without Hamlib installed, Find my radio reported a found radio as unverified because the
  rigctl tests could not run. A radio that named itself over the serial port now counts as
  verified, the Connection panel is filled in, and the handoff is shown; the note says to
  install WSJT-X or Hamlib for the full tests
- The status bar now explains that Run Tests needs Hamlib because the tests are Hamlib
  commands, and that Find my radio and Scan PC work without it

### Changed
- Icom baud order is now 115200 first (the IC-7300's USB default), then 19200, 9600, 4800; the
  IC-7300 preset uses 115200
- The reply window waits a little longer for silence so an echo and the answer behind it are
  read as one exchange

## [0.6.9] - 2026-09-21

Find my radio hands you the settings; Hamlib's dummy rig can no longer pass unnoticed.

### Added
- Settings handoff after a verified find: the exact fields for WSJT-X / JS8Call (File › Settings ›
  Radio), Fldigi (Rig Control › Hamlib), and Winlink Express, each as one copyable line
- RigCheck reads what WSJT-X and JS8Call are configured to use (`WSJT-X.ini`, `JS8Call.ini`):
  their port is probed first, their radio stands in when none is chosen, and after the sweep
  each program gets a one-line verdict — already matches, or exactly which fields to change
- The raw console warns beside every command sent to Hamlib model 1, the dummy rig

## [0.6.8] - 2026-09-21

Find my radio — the second half of the 0.7.0 milestone, first cut.

### Added
- **Find my radio** button: tick the COM ports it may open, and it sweeps each with read-only
  identify and read-frequency queries (Kenwood/Elecraft/Yaesu `ID;`, Icom CI-V, Yaesu 5-byte)
  across the likely baud rates, stops on the first radio that answers, verifies it with the
  normal Hamlib test suite, and fills in the Connection panel. rigctld already listening is
  reported as a working connection; Flrig is noted
- Every byte sent and received is shown in the results panel with a Copy button, and goes into
  Copy Results and Export Log under a "Find my radio transcript" heading, so the exchange can be
  replayed in a terminal program
- Data files anyone can extend by pull request: `rig_families.json` (which built-in query and
  which baud rates per protocol family), `rig_ids.json` (how each radio names itself → Hamlib
  model), `port_skip_patterns.json` (ports classed as rotator, amplifier, GPS, or Bluetooth start
  unticked). Data files choose a query by name; they can never contain command bytes
- Design document: `docs/discovery-flow.md` (with the diagram) and `docs/discovery-flow.drawio`
- Telemetry event `discovery` (ports swept, families/models/bauds that answered, verified count)

### Safety
- RTS and DTR are never asserted during discovery — on many interfaces they are the PTT line
- The sweep only runs from the button, only on ticked ports, and sends read commands only

## [0.6.7] - 2026-09-21

First half of the 0.7.0 milestone: the PC-side checks.

### Added
- **Scan PC** button: ten read-only checks that need no radio — Windows version, every Hamlib
  copy found and which one is used, `rigctl --version`, rigctl on PATH (with the `setx` fix
  shown), the selected radio model with a clear warning if it is Hamlib's dummy rig, serial
  driver problem codes, whether anything listens on the rigctld and Flrig ports, rigctld startup
  entries, Windows Firewall rules for rigctld, and installed radio software
- Scan results use the same results panel, Copy Results, and Export Log as the connection tests
- Completion notification also fires when a scan finishes

### Changed
- The results panel shows pending placeholders only for the checks about to run, instead of
  every test RigCheck knows about

## [0.6.6] - 2026-09-21

Results you can select and copy; a heads-up when a run finishes.

### Added
- Completion notification: the taskbar button flashes when a run finishes while the window is in
  the background, and a system sound can be played. Both are in Settings
- Diagnosis "Learn more" link and the suggested fix command are shown inline in the results

### Changed
- The results panel is now a document rather than a list: drag-select across tests, Ctrl+A,
  Ctrl+C. Copies are plain text so pasting into email or a forum brings no fonts or colors
- A failed test's diagnosis is always shown instead of behind an expand button
- Each command line is color-coded and has its own Copy button

## [0.6.5] - 2026-09-21

Every string translatable; translation contributor process in place.

### Added
- Copy Results button — the same plain-text report as Export Log, to the clipboard
- All remaining user-visible text moved into `Strings.resx`: test names and messages, every
  diagnosis and help topic, the exported log, status messages, and the main window
- Translation coverage workflow: the language table in `TRANSLATING.md` and the credits in
  `TRANSLATORS.md` are regenerated automatically whenever a string file changes
- Translator credit shown in Help > About, read from the active language's own file
- Translation issue template
- Screenshots in the README

### Changed
- Exported log labels are padded to a fixed width so translated labels stay aligned

### Fixed
- The app closed after Continue on the first-run diagnostics dialog, because that dialog was the
  first window shown and WPF treated it as the main window

## [0.6.4] - 2026-09-21

Radio and cable databases as data files.

### Added
- `Assets/radio_presets.json` (13 rigs, ordered by popularity) and `Assets/usb_devices.json`
  (9 USB-serial chips with cable hints and suggested presets), embedded in the exe
- Data files can be overridden without a rebuild: a copy next to the exe, in `Assets\` beside it,
  or in `%LOCALAPPDATA%\ShackDesk\Data\` takes precedence over the embedded one
- Presets for Yaesu FT-891 and Elecraft KX2
- A cable hint in the USB database may be a translation key or plain text

### Changed
- Presets are no longer compiled into the program
- Alpha release tags use the same `yyyyMMdd` stamp as the filename

### Removed
- The "Flex 6300" preset, which pointed at Hamlib model 1 — the dummy rig — and would have
  reported every test as passing without touching a radio

### Fixed
- Five presets had the wrong Hamlib model number and would have driven the wrong radio protocol:
  FT-DX10 (1053 → 1042), FT-DX101D (1049 → 1040), TS-590SG (2031 → 2037),
  TS-890S (2047 → 2041), KX3 (2030 → 2045). All 13 now verified against Hamlib's `riglist.h`.

## [0.6.3] - 2026-09-21

Expiry, diagnostics, settings, and automated alpha releases.

### Added
- Every push to `develop` is published automatically as a dated alpha pre-release; alpha releases
  older than 30 days are removed. A weekly scheduled build keeps a current alpha available.
- Build channel and date stamped into the assembly at compile time; alpha builds expire
  30 days after they are built and refuse to start afterwards, beta builds warn after 90 days,
  stable builds never expire
- Expiry date shown in the window title, in Help > About, and in the status strip during
  the final nine days
- Help > Download latest build (opens the releases page)
- Alpha/beta/stable channels and expiry documented in the README
- Anonymous diagnostics, opt-in via a first-run prompt or Settings, sent to the shared ShackDesk
  telemetry endpoint: a startup report and a per-test-run report with radio model, serial settings,
  USB cable identifiers, and test outcomes. Every report is stored locally and viewable under
  Help > View collected data. Support ID shown in Help > About.
- Settings window: diagnostics toggle, log level (Off / Errors only / Normal / Detailed —
  Detailed by default on test builds), log folder path with Open and Delete buttons, Support ID
  with Copy and Reset. Changes apply on OK; Cancel discards them.
- Unhandled exceptions are logged, reported if diagnostics are on, and shown to the operator
  before the app closes

### Changed
- Help > Contents opens the RigCheck product page instead of the site's general FAQ
- CI artifacts are named `RigCheck-{version}-{channel}-{date}-{sha}.exe` and the SHA-256 file
  lists the filename it applies to
- `actions/setup-dotnet` updated to v5 (Node 20 runtime deprecation)

## [0.6.2] - 2026-09-21

First launch feedback and the move to .NET 10.

### Added
- Menu bar: Settings (placeholder) and Help with Contents (F1, opens the online FAQ) and About
- About window showing branding, version, developer, license, and links
- Localization foundation: `Strings.resx` with a `Strings` accessor and `{loc:Str}` XAML markup extension. New UI text is in the resource file; existing text migrates next.
- External-link glyph on every control that opens a web browser
- Placeholder text in the radio and COM port dropdowns when nothing is selected
- Status strip tells the operator what still needs choosing before tests can run
- Dependabot, CodeQL analysis, and dependency review workflows

### Changed
- Target runtime moved from .NET 8 to .NET 10 (LTS, supported through 2028-11-14).
  No change to supported Windows versions; see the Platform section of the README.
- Microsoft.Extensions, System.Management, System.IO.Ports, and Serilog.Extensions.Hosting
  packages updated to their 10.x releases
- Radio and COM port start unselected instead of defaulting to Hamlib's dummy rig and the first port.
  Run Tests is disabled until both are chosen.
- Baud rate, PTT, data bits, parity, stop bits, and flow control default to "Radio default",
  which omits the setting so Hamlib uses the model's own defaults
- Dropdowns use a dark-themed template; items and the selected value are readable
- Serial-setting dropdowns use minimum widths so translated labels fit

### Fixed
- S-meter reading interpreted Hamlib's dB-relative-to-S9 value as dBm, so an S9 signal displayed as "S9+73"

## [0.6.1] - 2026-09-21

First successful build. The application compiles in CI and produces a
self-contained executable.

### Added
- CI build workflow producing the self-contained exe and its SHA-256 as an artifact
- `app.manifest` — runs as standard user, per-monitor DPI aware
- Placeholder application icon (to be replaced with final artwork)
- Help topics shown alongside timeout and no-response failures
- Main window code-behind: window position persistence, console Enter/Up/Down key handling
- Raw console command history persists across sessions
- Dark theme: color palette, control styles, and value converters for the main window

### Changed
- Zoom applies a layout transform to the window content instead of a render transform on the window, so content reflows rather than clipping
- `DiagnosticResult` and `HelpTopic` moved to the Models namespace
- Zoom buttons bind to ViewModel commands instead of code-behind handlers

### Removed
- Dependency-injection registrations for services and ViewModels not yet implemented

### Fixed
- rigctl timeout was never detected — `Task.WaitAsync` throws on timeout rather than returning false
- Missing `System.IO.Ports` and `Serilog.Extensions.Hosting` package references

## [0.6.0] - 2026-09-20

Project structure and licensing milestone. No functional changes to the application.

### Added
- GPLv3 license
- `LEGAL.md` with trademark restriction, third-party trademark notices, and library attributions
- `CLA.md` and `CONTRIBUTING.md` with code standards and pull request process
- Pull request template and security policy
- `develop` integration branch; `main` is reserved for released code

### Changed
- Suite and app URLs now point to `shackdesk.com`
- Repository URLs use the `ShackDesk-RigCheck` name
- PATH helper commands are plain string literals
- README gains license badge, Contributing, and License sections

### Removed
- `CommercialTier` branding constant and `LicenseService` registration — monetization is deferred until closer to a stable release

### Fixed
- Missing `System.Management` package reference required by COM port enumeration

## [0.5.0] - 2026-04-07

Initial scaffold: WPF/MVVM project structure, service layer for Hamlib location and
invocation, COM port enumeration, diagnosis engine, settings, and log export. Feature
specification in README.
