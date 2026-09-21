# Changelog

All notable changes to RigCheck are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Version numbers: patch (x.x.1) for landed features and fixes, minor (x.1.0) for milestones.

## [Unreleased]

### Added
- Build channel and date stamped into the assembly at compile time; alpha builds expire
  30 days after they are built and refuse to start afterwards, beta builds warn after 90 days,
  stable builds never expire
- Expiry date shown in the window title, in Help > About, and in the status strip during
  the final nine days
- Help > Download latest build (opens the releases page)
- Alpha/beta/stable channels and expiry documented in the README

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
