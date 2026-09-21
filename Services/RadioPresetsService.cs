using RigCheck.Models;
using Serilog;
using Newtonsoft.Json;

namespace RigCheck.Services;

/// <summary>
/// Quick-start presets for popular radios, loaded from radio_presets.json
/// through DataFileLocator (an external copy overrides the embedded one).
/// Presets are ordered by Popularity so the most common rigs come first.
///
/// Selecting a preset fills all connection configuration fields; the
/// operator can override any field afterwards.
/// </summary>
public class RadioPresetsService
{
    private const string FileName = "radio_presets.json";

    private readonly List<RadioPreset> _presets;

    public IReadOnlyList<RadioPreset> Presets => _presets;

    public RadioPresetsService()
    {
        _presets = Load();
    }

    public RadioPreset? FindByName(string name) =>
        _presets.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ── Loading ───────────────────────────────────────────────────────────

    private static List<RadioPreset> Load()
    {
        try
        {
            var json = DataFileLocator.ReadAllText(FileName);
            if (json is null) return [];

            // Entries with a _comment key are documentation for contributors, not presets.
            var presets = JsonConvert.DeserializeObject<List<RadioPreset>>(json)?
                .Where(p => !string.IsNullOrEmpty(p.Name) && p.HamlibModelId > 1)
                .OrderByDescending(p => p.Popularity)
                .ThenBy(p => p.Name)
                .ToList() ?? [];

            Log.Information("Loaded {Count} radio presets", presets.Count);
            return presets;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load {File} — no presets available", FileName);
            return [];
        }
    }
}
