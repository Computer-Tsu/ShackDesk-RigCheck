using Newtonsoft.Json;
using RigCheck.Models;
using Serilog;

namespace RigCheck.Services;

/// <summary>
/// Loads the declarative rig knowledge Find my radio runs on:
/// rig_families.json, rig_ids.json, and port_skip_patterns.json, each via
/// DataFileLocator so an external copy beside the exe overrides the
/// embedded one. Entries with a _comment key are documentation and are
/// dropped on load.
/// </summary>
public class DiscoveryDataService
{
    public IReadOnlyList<RigFamily>       Families     { get; }
    public IReadOnlyList<RigIdEntry>      Ids          { get; }
    public IReadOnlyList<PortSkipPattern> SkipPatterns { get; }

    public DiscoveryDataService()
    {
        Families     = Load<RigFamily>("rig_families.json",       f => !string.IsNullOrEmpty(f.Id) && ProbeOperations.TryParse(f.Probe, out _));
        Ids          = Load<RigIdEntry>("rig_ids.json",           i => !string.IsNullOrEmpty(i.Reply) && i.HamlibModelId > 1);
        SkipPatterns = Load<PortSkipPattern>("port_skip_patterns.json", p => !string.IsNullOrEmpty(p.Pattern));
    }

    public RigFamily? Family(string id) =>
        Families.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The family a Hamlib model number belongs to, by its backend (thousands) digit.</summary>
    public RigFamily? FamilyForModel(int hamlibModelId) =>
        hamlibModelId <= 1 ? null
        : Families.FirstOrDefault(f => f.HamlibBackends.Contains(hamlibModelId / 1000));

    /// <summary>The family a cable's RadioFamily hint (from usb_devices.json) points at.</summary>
    public RigFamily? FamilyForVendor(string? vendor) =>
        string.IsNullOrEmpty(vendor) ? null
        : Families.FirstOrDefault(f => f.VendorHints.Any(v => v.Equals(vendor, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// All rigs that answer with this identity in this family. Several is
    /// legitimate (Elecraft K3/KX3/KX2 share ID017;); the first is the default.
    /// </summary>
    public IReadOnlyList<RigIdEntry> Lookup(string familyId, string reply) =>
        Ids.Where(i => i.Family.Equals(familyId, StringComparison.OrdinalIgnoreCase)
                    && i.Reply.Equals(reply, StringComparison.OrdinalIgnoreCase))
           .ToList();

    /// <summary>The device class a port name matches, or null when it looks like fair game.</summary>
    public string? SkipClass(string friendlyName) =>
        SkipPatterns.FirstOrDefault(p => friendlyName.Contains(p.Pattern, StringComparison.OrdinalIgnoreCase))?.DeviceClass;

    // ── Loading ───────────────────────────────────────────────────────────

    private static List<T> Load<T>(string fileName, Func<T, bool> keep)
    {
        try
        {
            var json = DataFileLocator.ReadAllText(fileName);
            if (json is null) return [];
            var items = JsonConvert.DeserializeObject<List<T>>(json)?.Where(keep).ToList() ?? [];
            Log.Information("Loaded {Count} entries from {File}", items.Count, fileName);
            return items;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load {File}", fileName);
            return [];
        }
    }
}
