using RigCheck.Models;
using Serilog;
using System.IO;

namespace RigCheck.Services;

/// <summary>
/// Reads what the operator's own digital-mode programs are already
/// configured to use. This is the best clue Find my radio has: most of the
/// time the model is right and only the port or the speed is wrong, and
/// "WSJT-X says COM5 at 9600, but the radio answered on COM3 at 19200" is
/// exactly the sentence the operator needs.
///
/// Read-only, file-based, no process interaction. Handles WSJT-X and
/// JS8Call (same INI layout). Fldigi stores baud as a list index and the
/// model as its own numbering, so it is left for a later pass.
/// </summary>
public class ConfigClueService
{
    private readonly RadioPresetsService  _presets;
    private readonly DiscoveryDataService _data;

    public ConfigClueService(RadioPresetsService presets, DiscoveryDataService data)
    {
        _presets = presets;
        _data    = data;
    }

    /// <summary>Every configuration RigCheck could read, in the order it looked.</summary>
    public IReadOnlyList<ConfigClue> ReadAll()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var clues = new List<ConfigClue>();

        foreach (var (app, path) in new[]
        {
            ("WSJT-X",  Path.Combine(local, "WSJT-X",  "WSJT-X.ini")),
            ("JS8Call", Path.Combine(local, "JS8Call", "JS8Call.ini")),
        })
        {
            var clue = ReadWsjtxStyle(app, path);
            if (clue is not null) clues.Add(clue);
        }
        return clues;
    }

    // ── WSJT-X / JS8Call ──────────────────────────────────────────────────
    // [Configuration]
    // Rig=Icom IC-7300          Hamlib's rig name, or "Hamlib NET rigctl"
    // CATSerialPort=COM3
    // CATSerialRate=19200
    // CATDataBits=Default|Seven|Eight
    // CATStopBits=Default|One|Two
    // CATHandshake=Default|None|XonXoff|Hardware
    // PTTMethod=CAT|DTR|RTS|VOX
    // NetworkServer=localhost:4532   (rigctl mode)

    private ConfigClue? ReadWsjtxStyle(string app, string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var cfg = ReadIniSection(path, "Configuration");
            if (cfg.Count == 0) return null;

            var rigName = cfg.GetValueOrDefault("Rig", string.Empty);
            if (string.IsNullOrEmpty(rigName) || rigName == "None") return null;

            var useRigctld = rigName.Contains("NET rigctl", StringComparison.OrdinalIgnoreCase);
            int.TryParse(cfg.GetValueOrDefault("CATSerialRate", "0"), out var baud);

            return new ConfigClue(
                App:        app,
                FilePath:   path,
                RigName:    rigName,
                ModelId:    useRigctld ? 2 : ResolveModel(rigName),
                Port:       useRigctld ? cfg.GetValueOrDefault("NetworkServer", "localhost:4532")
                                       : cfg.GetValueOrDefault("CATSerialPort", string.Empty),
                Baud:       baud,
                DataBits:   cfg.GetValueOrDefault("CATDataBits", "Default"),
                StopBits:   cfg.GetValueOrDefault("CATStopBits", "Default"),
                Handshake:  cfg.GetValueOrDefault("CATHandshake", "Default"),
                PttMethod:  cfg.GetValueOrDefault("PTTMethod", "CAT"),
                UseRigctld: useRigctld);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read {App} configuration at {Path}", app, path);
            return null;
        }
    }

    // WSJT-X stores Hamlib's display name ("Icom IC-7300"). Presets and the
    // identity table use the same convention, so a name match is usually
    // exact; fall back to a loose contains-match on the model part.
    private int ResolveModel(string rigName)
    {
        var preset = _presets.Presets.FirstOrDefault(p => p.Name.Equals(rigName, StringComparison.OrdinalIgnoreCase))
                  ?? _presets.Presets.FirstOrDefault(p => rigName.Contains(ModelPart(p.Name), StringComparison.OrdinalIgnoreCase));
        if (preset is not null) return preset.HamlibModelId;

        var id = _data.Ids.FirstOrDefault(i => i.Name.Equals(rigName, StringComparison.OrdinalIgnoreCase))
              ?? _data.Ids.FirstOrDefault(i => rigName.Contains(ModelPart(i.Name), StringComparison.OrdinalIgnoreCase));
        return id?.HamlibModelId ?? 0;
    }

    // "Icom IC-7300" → "IC-7300"
    private static string ModelPart(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[(space + 1)..] : name;
    }

    private static Dictionary<string, string> ReadIniSection(string path, string section)
    {
        var result  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inScope = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;

            if (line[0] == '[')
            {
                inScope = line.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inScope) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            result[line[..eq].Trim()] = DecodeQtValue(line[(eq + 1)..].Trim());
        }
        return result;
    }

    // WSJT-X writes enum settings as Qt variants, e.g.
    //   @Variant(\0\0\0\x7f\0\0\0\x1eTransceiverFactory::PTTMethod\0\0\0\0\xfPTT_method_VOX\0)
    //   @Variant(...TransceiverFactory::StopBits\0\0\0\0\x12\x64\x65\x66\x61ult_stop_bits\0)
    // Unescape \0 and \xHH (Qt escapes any hex-looking character that
    // follows an escape, so a greedy hex read is safe), split on NUL, and
    // take the last segment minus its length-prefix byte. Then strip the
    // type prefixes/suffixes so callers see "VOX", "two", "none", "default".
    private static string DecodeQtValue(string raw)
    {
        if (!raw.StartsWith("@Variant(", StringComparison.Ordinal)) return raw;

        var inner = raw[9..].TrimEnd(')');
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length)
            {
                if (inner[i + 1] == '0') { sb.Append('\0'); i++; continue; }
                if (inner[i + 1] == 'x')
                {
                    int j = i + 2; var hex = string.Empty;
                    while (j < inner.Length && hex.Length < 4 && Uri.IsHexDigit(inner[j])) hex += inner[j++];
                    if (hex.Length > 0) { sb.Append((char)Convert.ToInt32(hex, 16)); i = j - 1; continue; }
                }
            }
            sb.Append(inner[i]);
        }

        var segments = sb.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var last = segments.Length > 0 ? segments[^1] : string.Empty;
        last = new string(last.SkipWhile(c => !char.IsLetter(c) && c != '_').ToArray());

        return last
            .Replace("PTT_method_", string.Empty)
            .Replace("handshake_",  string.Empty)
            .Replace("_stop_bits",  string.Empty)
            .Replace("_stop_bit",   string.Empty)
            .Replace("_data_bits",  string.Empty);
    }
}
