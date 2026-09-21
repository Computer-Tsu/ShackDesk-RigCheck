RigCheck is the second ShackDesk suite application.

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

"Know your rig is ready."<br>
Serial CAT communication diagnostics using Hamlib. Verify your radio connection before it matters.

## Suite Context

Brand: ShackDesk<br>
Developer: Mark McDow N4TEK / My Computer Guru LLC<br>
GitHub: github.com/Computer-Tsu<br>
Suite site: shackdesk.com<br>
Technology: C# WPF .NET 10, MVVM architecture,
            dependency injection, Serilog logging,
            GitHub Actions CI/CD, no local compiler

## Platform

RigCheck runs on Windows 10 (version 21H2 or later)
and Windows 11, 64-bit. Nothing else needs to be
installed: the .NET runtime is bundled inside the
single RigCheck.exe.

Windows 11 on ARM runs it through the built-in x64
emulation. Windows 7 and 8.1 are not supported.

### Why .NET 10

RigCheck moved from .NET 8 to .NET 10 in September
2026. This changed nothing about which versions of
Windows it runs on — .NET 8 and .NET 10 support the
identical list of Windows client versions, and
neither supports Windows 7 or 8.1.

The reason is support lifetime. .NET 8 reaches end
of support on November 10, 2026, after which the
runtime bundled inside every RigCheck build would
stop receiving security fixes. .NET 10 is the
current long-term-support release, supported through
November 14, 2028.

### Build channels and expiry

RigCheck is published in three channels:

- **Alpha** — built from every change on the
  `develop` branch. **Alpha builds stop running
  30 days after they were built.** This keeps
  testers on current code and steers everyday
  users toward stable releases. The expiry date
  is shown in the window title and in Help >
  About. When an alpha expires, starting it shows
  a notice with a link to the latest build.
- **Beta** — tagged pre-releases. Beta builds warn
  in the status bar 90 days after they were built
  but keep running.
- **Stable** — tagged releases. Never expire.

The window title shows the version, channel, and
expiry date, for example
`RigCheck by ShackDesk 0.6.2-alpha — expires 2026-10-21`.

### Where RigCheck keeps its files

RigCheck is a single portable exe and writes only
to your local application data folder. Nothing goes
in the registry.

```
%LOCALAPPDATA%\ShackDesk\RigCheck\
    rigcheck-settings.json   all settings, plain JSON
    Logs\                    daily log files, 7 kept
    Telemetry\               local copies of diagnostic reports
```

Delete `rigcheck-settings.json` to reset every
setting to its default. Settings › Logging shows
the log folder and can open or empty it.

### Anonymous diagnostics

On first launch RigCheck asks whether it may send
anonymous diagnostic reports. Nothing is sent
unless you say yes, and you can change the choice
in Settings at any time. Every report is also
stored locally and can be inspected under
Help > View collected data.

What is sent: the RigCheck and Windows versions,
whether Hamlib was found, and after each test run
the radio model, serial settings, USB cable
identifiers, and which tests passed or failed.
This is what improves the radio and cable
database for everyone.

Never sent: callsign, computer name, file paths,
serial numbers, or IP address. The only identifier
is a random ID created on first run, shown in
Help > About as a Support ID. Reports go to the
shared ShackDesk endpoint; see
shackdesk.com/privacy for the full policy.

### Hamlib

RigCheck does not include Hamlib. It finds the
rigctl.exe already on the computer from any of:

- WSJT-X (includes Hamlib) — wsjt.sourceforge.io
- Fldigi (includes Hamlib) — w1hkj.com
- Standalone Hamlib for Windows —
  github.com/Hamlib/Hamlib/releases

Most operators already have WSJT-X installed.

## RigCheck Purpose

Hamlib is the open source radio control library used
by virtually every digital mode program — WSJT-X, 
Fldigi, JS8Call, Winlink, and dozens more. When rig 
control does not work, diagnosing why is genuinely 
difficult for non-technical operators. Currently 
they must use cryptic command line tools.

RigCheck provides a friendly GUI front-end for 
testing and diagnosing Hamlib rig control connections.

Target users:
- Primary: Ham radio operators troubleshooting 
  why their rig control is not working in WSJT-X,
  Fldigi, JS8Call, Winlink, or other digital mode 
  software
- Secondary: Ham radio developers testing Hamlib 
  integration

## Core Features

### Connection Configuration
- Radio model selector (searchable dropdown 
  populated from Hamlib rig database)
- COM port selector (populated from system, 
  inherits detection logic from PortPane if installed)
- Baud rate selector: 4800/9600/19200/38400/
  57600/115200
- Data bits, parity, stop bits (default 8N1)
- Flow control: None/Hardware/Software
- PTT method: CAT/RTS/DTR/VOX/None
- Network mode: rigctld host:port (default 
  localhost:4532)
- Connection type toggle: Direct serial vs 
  rigctld network
- Detect and test GPS (USB serial receivers, puck)

### Diagnostic Test Suite
Run a sequence of standard Hamlib queries and 
display pass/fail results in plain English:

Test 1: Open connection
  Pass: "Connected to [radio model] on [port]"
  Fail: diagnose reason (port busy, wrong baud, 
        no response, wrong model, timeout)

Test 2: Get frequency
  Pass: "VFO A frequency: 14.074 MHz"
  Fail: "Radio did not respond to frequency query"

Test 3: Get mode
  Pass: "Current mode: USB, passband: 3000 Hz"
  Fail: with diagnostic suggestion

Test 4: Get PTT state
  Pass: "PTT is off (transmitter not keyed)"
  Fail: with diagnostic suggestion

Test 5: Get signal meter (S-meter)
  Pass: "Signal strength: S7 (-85 dBm)"
  Fail: with diagnostic suggestion

Test 6: Get VFO
  Pass: "Active VFO: VFO A"
  Fail: with diagnostic suggestion

Test 7: Set and verify frequency (optional)
  User confirms before transmitter-affecting tests
  Sets a nearby frequency, reads back, verifies match
  Restores original frequency afterward

### Failure Diagnosis Engine
Map common failure patterns to plain English causes:

No response at all:
→ "No response from radio. Check: 
   Is the radio powered on?
   Is the correct COM port selected?
   Is the baud rate correct for your radio?
   Is another program already using this port?"

Port already in use:
→ "COM4 is already open by another program.
   Check if WSJT-X, Fldigi, or another 
   digital mode program is running."

Wrong radio model:
→ "Radio responded but returned unexpected data.
   The selected radio model may not match your 
   actual radio. Try searching for your exact 
   model number."

Timeout:
→ "Connection timed out. Check your cable 
   connection and verify the radio is set to 
   accept CAT commands. Consult your radio 
   manual for CAT/CI-V setup instructions."

rigctld not running:
→ "Could not connect to rigctld on localhost:4532.
   Verify rigctld is running and check the 
   host and port settings."

### Raw Command Console
Collapsible advanced panel for technical users:
- Text input to send raw Hamlib commands
- Response display
- Command history (up arrow recalls previous)
- Labeled: "Advanced — raw Hamlib commands"

### Common Radio Quick-Start Presets
Dropdown of popular radios with known-good settings:
- Icom IC-7300: USB, CI-V, 19200 baud
- Icom IC-705: USB, CI-V, 115200 baud  
- Yaesu FT-991A: USB, 38400 baud
- Yaesu FT-DX10: USB, 38400 baud
- Kenwood TS-590SG: USB, 115200 baud
- Elecraft K3: USB, 38400 baud
Selecting a preset fills all connection fields.
User can override any field after preset applied.

### Log Export
Save test results to a text file the user can 
email to a club Elmer or post to a support forum.
Format: plain text, human readable, not JSON.
Include: timestamp, radio model, port settings,
each test result, any error messages.

## Contributing

Radio and cable database entries, bug reports with
an exported log, and code are all welcome. See
[CONTRIBUTING.md](CONTRIBUTING.md).

## License

RigCheck is licensed under the GNU General Public
License, version 3. See [LICENSE](LICENSE).

RigCheck and ShackDesk are trademarks of My Computer
Guru LLC. See [LEGAL.md](LEGAL.md) for trademark and
third-party notices.

