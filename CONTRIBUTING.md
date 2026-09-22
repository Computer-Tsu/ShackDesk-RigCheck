# Contributing to RigCheck

**You do not need to be a programmer to contribute.**
Some of the most valuable contributions are radio and cable database entries - a Hamlib model number, the baud rate that worked, and the USB VID/PID from Device Manager.
If you got your rig talking and RigCheck did not already know the right settings, that is exactly what the project needs.

---

Thank you for your interest in contributing to RigCheck by ShackDesk.

## Ways to Contribute

| Type | How |
| ------ | ----- |
| Bug report | Open an issue with your exported RigCheck log attached |
| Feature request | Open an issue describing the problem you are trying to solve |
| Radio, cable, or protocol data | Edit a JSON file under `Assets/` and submit a PR - see [Data files](#data-files); no compiler needed |
| Translation | See [TRANSLATING.md](TRANSLATING.md) - no programming needed |
| Code | Fork, branch, and submit a pull request (see below) |

## Data files

Most of what RigCheck knows about radios lives in JSON, not code, so anyone can add a rig by pull request.
Validate at jsonlint.com before submitting; each file starts with a `_comment` entry that explains its fields.

| File | What it holds |
| ------ | ----- |
| `Assets/radio_presets.json` | Quick-start presets: Hamlib model, default serial settings, popularity (sort order) |
| `Assets/usb_devices.json` | USB VID/PID → serial chip, cable hint, radio family, suggested presets |
| `Assets/rig_families.json` | Protocol families for Find my radio: which built-in query to send, baud rates to sweep (most likely first), default model |
| `Assets/rig_ids.json` | How a radio names itself (`ID023;`, CI-V address `94`) → Hamlib model number |
| `Assets/port_skip_patterns.json` | Port-name patterns classed as rotator, amplifier, antenna, GPS, or Bluetooth - never opened by Find my radio |

Hamlib model numbers follow `riglist.h` in the Hamlib source: the thousands digit is the backend (1 Yaesu, 2 Kenwood and Elecraft, 3 Icom).
Please say where a value came from (the radio's manual, `rigctl -l`, a real exchange) in the PR.

**One rule that will not bend:**
data files select a probe operation by name from the fixed set compiled into RigCheck.
A PR that adds command bytes, a new field carrying bytes, or a "custom command" mechanism to any data file will be declined.
A data file that could make RigCheck send arbitrary bytes at a transmitter is a safety problem, not a feature.

## CLA

All contributors must agree to the [Contributor License Agreement](CLA.md) before a pull request can be merged.
By submitting a PR, you agree to its terms.

## Code Standards

- **MVVM strictly enforced.** No business logic in code-behind files (`.xaml.cs`). All logic lives in ViewModels and Services.
- **No hardcoded strings.** All branding values reference `BrandingInfo` constants.
- **No admin rights.** The app must run as standard user (`asInvoker`).
- **Logging via Serilog only.** No `Console.WriteLine`, `Debug.WriteLine`, or `Trace`.
- **Offline-first.** Nothing may block startup or a diagnostic run on network access. Network features fail silently when offline.
- **Query-only during discovery.** Code that probes an unidentified serial device must never send a command that changes radio state.
- **.NET 10** target only. See the Platform section of the README for why.

## Pull Request Process

1. Fork the repository
2. Create a branch from `develop`: `feature/my-feature` or `fix/my-bug`
3. Make your changes
4. Submit a pull request against `develop` - fill out the PR template fully
5. CI builds and tests every PR; a maintainer will review within 7 days

## Questions?

Post in [GitHub Discussions](https://github.com/Computer-Tsu/ShackDesk-RigCheck/discussions).
