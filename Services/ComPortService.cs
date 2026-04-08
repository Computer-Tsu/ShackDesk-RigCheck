using RigCheck.Models;
using Serilog;
using System.IO;
using System.Management;
using System.IO.Ports;
using Newtonsoft.Json;

namespace RigCheck.Services;

/// <summary>
/// Enumerates available COM ports with friendly names and, where possible,
/// a radio family hint derived from the USB VID/PID of the connected device.
///
/// Example hint: "Icom CI-V USB cable detected — try IC-7300 or IC-705"
///
/// This is the primary overlap with PortPane: COM port enumeration and
/// the USB device lookup table. When ShackDesk.Core exists, this moves
/// there. For now it lives here and is kept in sync with PortPane manually.
/// </summary>
public class ComPortService
{
    private readonly List<UsbDeviceEntry> _usbDb;

    public ComPortService()
    {
        _usbDb = LoadUsbDatabase();
    }

    // ── Public API ────────────────────────────────────────────────────────

    public IReadOnlyList<ComPortInfo> GetAvailablePorts()
    {
        try
        {
            return GetPortsViaWmi();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WMI COM port enumeration failed, using fallback");
            return GetPortsFallback();
        }
    }

    // ── WMI enumeration ───────────────────────────────────────────────────

    private List<ComPortInfo> GetPortsViaWmi()
    {
        var ports = new List<ComPortInfo>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

        foreach (ManagementObject obj in searcher.Get())
        {
            var name  = obj["Name"]?.ToString()        ?? string.Empty;
            var pnpId = obj["PNPDeviceID"]?.ToString() ?? string.Empty;

            var portName = ExtractComPortName(name);
            if (portName is null) continue;

            var (vid, pid) = ExtractVidPid(pnpId);
            var usbEntry   = vid is not null ? LookupUsb(vid, pid) : null;
            var deviceType = ClassifyDevice(name, pnpId);

            ports.Add(new ComPortInfo(
                PortName:         portName,
                FriendlyName:     name,
                DeviceType:       deviceType,
                IsUsbSerial:      deviceType == "USB-Serial",
                Vid:              vid,
                Pid:              pid,
                CableHint:        usbEntry?.CableHint,
                RadioFamily:      usbEntry?.RadioFamily,
                SuggestedPresets: usbEntry?.SuggestedPresets ?? []));
        }

        // Known radio cables first, then other USB-serial, then by port number
        ports.Sort((a, b) =>
        {
            var aKnown = a.RadioFamily is not null ? 0 : 1;
            var bKnown = b.RadioFamily is not null ? 0 : 1;
            if (aKnown != bKnown) return aKnown.CompareTo(bKnown);
            if (a.IsUsbSerial != b.IsUsbSerial) return a.IsUsbSerial ? -1 : 1;
            return ExtractPortNumber(a.PortName).CompareTo(ExtractPortNumber(b.PortName));
        });

        Log.Debug("WMI found {Count} COM port(s)", ports.Count);
        return ports;
    }

    // ── Fallback ──────────────────────────────────────────────────────────

    private static List<ComPortInfo> GetPortsFallback()
    {
        return SerialPort.GetPortNames()
            .OrderBy(ExtractPortNumber)
            .Select(p => new ComPortInfo(
                PortName:         p,
                FriendlyName:     p,
                DeviceType:       "Unknown",
                IsUsbSerial:      false,
                Vid:              null,
                Pid:              null,
                CableHint:        null,
                RadioFamily:      null,
                SuggestedPresets: []))
            .ToList();
    }

    // ── USB database ──────────────────────────────────────────────────────

    private static List<UsbDeviceEntry> LoadUsbDatabase()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "usb_devices.json"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "usb_devices.json"),
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var db   = JsonConvert.DeserializeObject<List<UsbDeviceEntry>>(json);
                if (db is not null)
                {
                    Log.Debug("USB device database: {Count} entries from {Path}", db.Count, path);
                    return db;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load USB device database — cable hints unavailable");
        }

        return [];
    }

    private UsbDeviceEntry? LookupUsb(string vid, string? pid)
    {
        // Prefer exact VID+PID match, fall back to VID-only
        return _usbDb.FirstOrDefault(e =>
                   e.Vid.Equals(vid, StringComparison.OrdinalIgnoreCase) &&
                   (pid is null || e.Pid.Equals(pid, StringComparison.OrdinalIgnoreCase)))
            ?? _usbDb.FirstOrDefault(e =>
                   e.Vid.Equals(vid, StringComparison.OrdinalIgnoreCase));
    }

    // ── Parsing helpers ───────────────────────────────────────────────────

    private static string? ExtractComPortName(string wmiName)
    {
        var start = wmiName.LastIndexOf('(');
        var end   = wmiName.LastIndexOf(')');
        if (start < 0 || end <= start) return null;
        var candidate = wmiName[(start + 1)..end];
        return candidate.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            ? candidate.ToUpper() : null;
    }

    /// <summary>
    /// Extract VID and PID from a Windows PnP device ID string.
    /// Example: USB\VID_10C4&amp;PID_EA60\0001
    /// </summary>
    private static (string? Vid, string? Pid) ExtractVidPid(string pnpId)
    {
        if (string.IsNullOrEmpty(pnpId)) return (null, null);

        string? vid = null, pid = null;

        var vidIdx = pnpId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
        if (vidIdx >= 0 && pnpId.Length >= vidIdx + 8)
            vid = pnpId.Substring(vidIdx + 4, 4);

        var pidIdx = pnpId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
        if (pidIdx >= 0 && pnpId.Length >= pidIdx + 8)
            pid = pnpId.Substring(pidIdx + 4, 4);

        return (vid?.ToUpper(), pid?.ToUpper());
    }

    private static string ClassifyDevice(string name, string pnpId)
    {
        var combined = (name + pnpId).ToUpperInvariant();
        if (combined.Contains("USB"))         return "USB-Serial";
        if (combined.Contains("BLUETOOTH"))   return "Bluetooth";
        if (combined.Contains("COMMUNICATIONS PORT")
         || combined.Contains("SERIAL PORT")) return "Physical";
        return "Other";
    }

    private static int ExtractPortNumber(string portName) =>
        int.TryParse(new string(portName.Where(char.IsDigit).ToArray()), out var n) ? n : 0;
}

// ── USB database entry ────────────────────────────────────────────────────

public class UsbDeviceEntry
{
    public string   Vid              { get; set; } = string.Empty;
    public string   Pid              { get; set; } = string.Empty;
    public string   Manufacturer     { get; set; } = string.Empty;
    public string   Chip             { get; set; } = string.Empty;

    /// <summary>Human-readable hint shown in the RigCheck UI next to the COM port.</summary>
    public string   CableHint        { get; set; } = string.Empty;

    /// <summary>Radio family, or null if the cable is generic (e.g. bare FTDI adapter).</summary>
    public string?  RadioFamily      { get; set; }

    /// <summary>Preset names from RadioPresetsService that are likely matches for this cable.</summary>
    public string[] SuggestedPresets { get; set; } = [];
}
