# Changelog

All notable changes to RigCheck are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Version numbers: patch (x.x.1) for landed features and fixes, minor (x.1.0) for milestones.

## [Unreleased]

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
