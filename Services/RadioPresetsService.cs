using RigCheck.Models;
using Serilog;
using System.IO;
using Newtonsoft.Json;

namespace RigCheck.Services;

/// <summary>
/// Provides quick-start presets for popular radios.
/// Built-in presets are compiled in; optional user-defined presets
/// can extend them from radio_presets.json in the assets folder.
///
/// Selecting a preset fills all connection configuration fields.
/// The user can override any field after applying a preset.
/// </summary>
public class RadioPresetsService
{
    private readonly List<RadioPreset> _presets;

    public IReadOnlyList<RadioPreset> Presets => _presets;

    public RadioPresetsService()
    {
        _presets = [..BuiltInPresets()];
        TryLoadUserPresets();
    }

    public RadioPreset? FindByName(string name) =>
        _presets.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ── Built-in presets ──────────────────────────────────────────────────
    // Hamlib model IDs: https://github.com/Hamlib/Hamlib/wiki/Supported-Radios
    // These are known-good settings from the community.

    private static IEnumerable<RadioPreset> BuiltInPresets() =>
    [
        new RadioPreset
        {
            Name          = "Icom IC-7300",
            Manufacturer  = "Icom",
            HamlibModelId = 3073,
            BaudRate      = 19200,
            PttMethod     = "CAT",
            Notes         = "CI-V address default 0x94. USB cable to radio USB port.",
        },
        new RadioPreset
        {
            Name          = "Icom IC-705",
            Manufacturer  = "Icom",
            HamlibModelId = 3085,
            BaudRate      = 115200,
            PttMethod     = "CAT",
            Notes         = "CI-V address default 0xA4. USB cable to radio USB port.",
        },
        new RadioPreset
        {
            Name          = "Icom IC-7610",
            Manufacturer  = "Icom",
            HamlibModelId = 3078,
            BaudRate      = 115200,
            PttMethod     = "CAT",
            Notes         = "CI-V address default 0x98.",
        },
        new RadioPreset
        {
            Name          = "Icom IC-9700",
            Manufacturer  = "Icom",
            HamlibModelId = 3081,
            BaudRate      = 115200,
            PttMethod     = "CAT",
            Notes         = "CI-V address default 0xA2.",
        },
        new RadioPreset
        {
            Name          = "Yaesu FT-991A",
            Manufacturer  = "Yaesu",
            HamlibModelId = 1035,
            BaudRate      = 38400,
            PttMethod     = "CAT",
            Notes         = "Use USB cable to radio USB port. Enable CAT in radio menu.",
        },
        new RadioPreset
        {
            Name          = "Yaesu FT-DX10",
            Manufacturer  = "Yaesu",
            HamlibModelId = 1053,
            BaudRate      = 38400,
            PttMethod     = "CAT",
            Notes         = "USB cable to radio USB port.",
        },
        new RadioPreset
        {
            Name          = "Yaesu FT-DX101D",
            Manufacturer  = "Yaesu",
            HamlibModelId = 1049,
            BaudRate      = 38400,
            PttMethod     = "CAT",
            Notes         = "USB cable to radio USB port.",
        },
        new RadioPreset
        {
            Name          = "Kenwood TS-590SG",
            Manufacturer  = "Kenwood",
            HamlibModelId = 2031,
            BaudRate      = 115200,
            FlowControl   = "Hardware",
            PttMethod     = "CAT",
            Notes         = "Enable hardware flow control (RTS/CTS) on radio.",
        },
        new RadioPreset
        {
            Name          = "Kenwood TS-890S",
            Manufacturer  = "Kenwood",
            HamlibModelId = 2047,
            BaudRate      = 115200,
            FlowControl   = "Hardware",
            PttMethod     = "CAT",
        },
        new RadioPreset
        {
            Name          = "Elecraft K3",
            Manufacturer  = "Elecraft",
            HamlibModelId = 2029,
            BaudRate      = 38400,
            PttMethod     = "CAT",
        },
        new RadioPreset
        {
            Name          = "Elecraft KX3",
            Manufacturer  = "Elecraft",
            HamlibModelId = 2030,
            BaudRate      = 38400,
            PttMethod     = "CAT",
        },
        new RadioPreset
        {
            Name          = "Flex 6300 (SmartSDR)",
            Manufacturer  = "FlexRadio",
            HamlibModelId = 1,   // Use rigctld / network mode for Flex
            BaudRate      = 4800,
            PttMethod     = "CAT",
            Notes         = "Flex radios typically use rigctld network mode. See SmartSDR documentation.",
        },
    ];

    // ── Optional user-defined presets ─────────────────────────────────────

    private void TryLoadUserPresets()
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory, "Assets", "radio_presets.json");

            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var userPresets = JsonConvert.DeserializeObject<List<RadioPreset>>(json);
            if (userPresets is null) return;

            _presets.AddRange(userPresets);
            Log.Information("Loaded {Count} user-defined radio presets", userPresets.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load user radio presets — using built-ins only");
        }
    }
}
