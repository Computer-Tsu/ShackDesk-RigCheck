RigCheck is the second ShackDesk suite application.

"Know your rig is ready."<br>
Serial CAT communication diagnostics using Hamlib. Verify your radio connection before it matters.

## Suite Context

Brand: ShackDesk
Developer: Mark McDow N4TEK / My Computer Guru LLC
GitHub: github.com/Computer-Tsu
Suite site: shackdesk.com
Technology: C# WPF .NET 8, MVVM architecture,
            dependency injection, Serilog logging,
            GitHub Actions CI/CD, no local compiler

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
  Pass: "Signal strength: S7 (-73 dBm)"
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

