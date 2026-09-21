# Contributing to RigCheck

**You do not need to be a programmer to contribute.** Some of the most valuable
contributions are radio and cable database entries — a Hamlib model number, the baud
rate that worked, and the USB VID/PID from Device Manager. If you got your rig talking
and RigCheck did not already know the right settings, that is exactly what the project
needs.

---

Thank you for your interest in contributing to RigCheck by ShackDesk.

## Ways to Contribute

| Type | How |
| ------ | ----- |
| Bug report | Open an issue with your exported RigCheck log attached |
| Feature request | Open an issue describing the problem you are trying to solve |
| Radio or cable database entry | Open an issue or submit a PR editing the data files under `Assets/` |
| Code | Fork, branch, and submit a pull request (see below) |

## CLA

All contributors must agree to the [Contributor License Agreement](CLA.md) before a pull request can be merged. By submitting a PR, you agree to its terms.

## Code Standards

- **MVVM strictly enforced.** No business logic in code-behind files (`.xaml.cs`). All logic lives in ViewModels and Services.
- **No hardcoded strings.** All branding values reference `BrandingInfo` constants.
- **No admin rights.** The app must run as standard user (`asInvoker`).
- **Logging via Serilog only.** No `Console.WriteLine`, `Debug.WriteLine`, or `Trace`.
- **Offline-first.** Nothing may block startup or a diagnostic run on network access. Network features fail silently when offline.
- **Query-only during discovery.** Code that probes an unidentified serial device must never send a command that changes radio state.
- **C# 12 / .NET 8** target only.

## Pull Request Process

1. Fork the repository
2. Create a branch from `develop`: `feature/my-feature` or `fix/my-bug`
3. Make your changes
4. Submit a pull request against `develop` — fill out the PR template fully
5. CI builds and tests every PR; a maintainer will review within 7 days

## Questions?

Post in [GitHub Discussions](https://github.com/Computer-Tsu/ShackDesk-RigCheck/discussions).
