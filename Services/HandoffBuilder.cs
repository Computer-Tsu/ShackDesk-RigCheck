using RigCheck.Localization;
using RigCheck.Models;

namespace RigCheck.Services;

/// <summary>
/// Stage 4 of Find my radio — the deliverable. Turns a verified rig into
/// the exact fields to enter in WSJT-X / JS8Call, Fldigi, and Winlink
/// Express, and compares them with what those programs are configured to
/// use today so the operator sees precisely what to change.
///
/// Field names are the programs' own labels. They are resources so a
/// translator can match a localized WSJT-X, but they are not RigCheck's
/// words to change.
/// </summary>
public class HandoffBuilder
{
    private readonly DiscoveryDataService _data;

    public HandoffBuilder(DiscoveryDataService data) => _data = data;

    public IReadOnlyList<TranscriptLine> Build(DiscoveredRig rig, IReadOnlyList<ConfigClue> clues)
    {
        var lines = new List<TranscriptLine>();
        var family   = _data.Family(rig.FamilyId);
        var stopBits = family?.HandoffStopBits ?? rig.HandoffStopBits;
        var ptt      = clues.FirstOrDefault(c => !string.IsNullOrEmpty(c.PttMethod))?.PttMethod ?? "CAT";

        lines.Add(new(TranscriptKind.Found, Strings.Get("Handoff_Heading")));

        if (rig.UseRigctld)
        {
            lines.Add(new(TranscriptKind.Note,    Strings.Get("Handoff_WsjtxPath")));
            lines.Add(new(TranscriptKind.Command, Strings.Format("Handoff_WsjtxNet", rig.Port)));
            lines.Add(new(TranscriptKind.Note,    Strings.Get("Handoff_FldigiPath")));
            lines.Add(new(TranscriptKind.Command, Strings.Format("Handoff_FldigiNet", rig.Port)));
        }
        else
        {
            lines.Add(new(TranscriptKind.Note,    Strings.Get("Handoff_WsjtxPath")));
            lines.Add(new(TranscriptKind.Command, Strings.Format("Handoff_Wsjtx", rig.ModelName, rig.Port, rig.Baud, stopBits, ptt)));
            lines.Add(new(TranscriptKind.Note,    Strings.Get("Handoff_FldigiPath")));
            lines.Add(new(TranscriptKind.Command, Strings.Format("Handoff_Fldigi", rig.ModelName, rig.Port, rig.Baud, stopBits)));
            lines.Add(new(TranscriptKind.Note,    Strings.Get("Handoff_WinlinkPath")));
            lines.Add(new(TranscriptKind.Command, Strings.Format("Handoff_Winlink", rig.ModelName, rig.Port, rig.Baud, ptt)));
        }

        // ── Configured vs. what actually works ────────────────────────────
        foreach (var clue in clues)
            lines.Add(new(TranscriptKind.Note, Compare(clue, rig)));

        return lines;
    }

    /// <summary>One sentence per program: matches, works via rigctl, or what to change.</summary>
    private static string Compare(ConfigClue clue, DiscoveredRig rig)
    {
        if (clue.UseRigctld)
            return Strings.Format("Handoff_DiffRigctl", clue.App, clue.RigName);

        if (rig.UseRigctld)
            return Strings.Format("Handoff_DiffSerialVsRigctld", clue.App, clue.RigName, clue.Port, clue.Baud);

        var changes = new List<string>();
        if (clue.ModelId != rig.HamlibModelId)
            changes.Add(Strings.Format("Handoff_DiffRig", rig.ModelName));
        if (!clue.Port.Equals(rig.Port, StringComparison.OrdinalIgnoreCase))
            changes.Add(Strings.Format("Handoff_DiffPort", rig.Port));
        if (clue.Baud != rig.Baud)
            changes.Add(Strings.Format("Handoff_DiffBaud", rig.Baud));

        return changes.Count == 0
            ? Strings.Format("Handoff_DiffMatch", clue.App)
            : Strings.Format("Handoff_DiffChange", clue.App, clue.RigName, clue.Port, clue.Baud, string.Join(", ", changes));
    }

    /// <summary>The opening line for each program whose settings were read, shown before the sweep.</summary>
    public static string Describe(ConfigClue clue) =>
        clue.UseRigctld
            ? Strings.Format("Handoff_ClueRigctl", clue.App, clue.Port)
            : Strings.Format("Handoff_Clue", clue.App, clue.RigName, clue.Port, clue.Baud);
}
