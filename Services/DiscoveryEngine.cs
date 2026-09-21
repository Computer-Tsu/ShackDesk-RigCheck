using RigCheck.Localization;
using RigCheck.Models;
using Serilog;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;

namespace RigCheck.Services;

/// <summary>What the operator asked Find my radio to look at.</summary>
public record DiscoveryRequest(
    IReadOnlyList<ComPortInfo> Ports,          // already ticked by the operator
    int  PreferredModelId  = 0,                // from the Connection panel, 0 = none
    int  PreferredBaud     = 0,                // 0 = radio default / unknown
    bool CheckNetwork      = true,             // rigctld and Flrig ports
    IReadOnlyList<ConfigClue> Clues = null!)   // what WSJT-X etc. are configured for
{
    public IReadOnlyList<ConfigClue> Clues { get; init; } = Clues ?? [];
}

/// <summary>
/// Find my radio, stages 0–2: inventory the ticked ports, rank protocol
/// families and baud rates per port, and probe each with query-only
/// exchanges until a rig answers. Emits every step as it happens so the
/// results panel can show the sweep live and the log can replay it.
///
/// See docs/discovery-flow.md for the design. Verification (stage 3) is
/// the ordinary test suite run by the caller against each DiscoveredRig.
/// </summary>
public class DiscoveryEngine
{
    private const int ReplyTimeoutMs = 300;   // how long to wait for the first byte
    private const int QuietMs        = 60;    // reply is complete after this much silence (echo and answer arrive as two frames)
    private const int AddressedTimeoutMs = 150; // CI-V answers within a few ms; the address sweep must stay quick

    private readonly DiscoveryDataService _data;
    private readonly RadioPresetsService  _presets;

    public DiscoveryEngine(DiscoveryDataService data, RadioPresetsService presets)
    {
        _data    = data;
        _presets = presets;
    }

    public async IAsyncEnumerable<ProbeEvent> RunAsync(
        DiscoveryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // ── Network transports first: instant, and "rigctld is already
        //    running" is the answer for a lot of WSJT-X setups.
        if (request.CheckNetwork)
        {
            foreach (var ev in CheckNetworkTransports())
                yield return ev;
        }

        // ── Serial ports, one at a time, handle held open across the sweep.
        foreach (var port in request.Ports)
        {
            ct.ThrowIfCancellationRequested();

            var skipClass = _data.SkipClass(port.FriendlyName);
            if (skipClass is not null)
            {
                yield return new(ProbeEventKind.Skipped, port.PortName,
                    Strings.Format("Disc_SkippedClass", port.PortName, skipClass));
                continue;
            }

            var candidates = RankCandidates(port, request);
            if (candidates.Count == 0) continue;

            using var probe = SerialProbe.TryOpen(port.PortName, candidates[0].Baud, out var failure);
            if (probe is null)
            {
                var kind = failure == "in use" ? ProbeEventKind.InUse : ProbeEventKind.Skipped;
                var text = failure == "in use"
                    ? Strings.Format("Disc_InUse", port.PortName)
                    : Strings.Format("Disc_OpenFailed", port.PortName, failure ?? string.Empty);
                yield return new(kind, port.PortName, text);
                continue;
            }

            DiscoveredRig? found = null;
            foreach (var (family, baud) in candidates)
            {
                ct.ThrowIfCancellationRequested();
                await probe.SetBaudAsync(baud, ct);

                yield return new(ProbeEventKind.Trying, port.PortName,
                    Strings.Format("Disc_Trying", port.PortName, baud, family.Name));

                var (rig, events) = await ProbeFamilyAsync(probe, port, family, baud, request, ct);
                foreach (var ev in events) yield return ev;

                if (rig is not null)
                {
                    found = rig;
                    yield return new(ProbeEventKind.Found, port.PortName,
                        Strings.Format("Disc_Found", rig.ModelName, rig.HamlibModelId, rig.Port, rig.Baud), Rig: rig);
                    break;   // one rig per port; on to the next port
                }
            }

            if (found is null)
                yield return new(ProbeEventKind.PortDone, port.PortName, Strings.Format("Disc_NoAnswer", port.PortName));
        }
    }

    // ── Stage 0: network ──────────────────────────────────────────────────

    private static IEnumerable<ProbeEvent> CheckNetworkTransports()
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();

        if (listeners.Any(l => l.Port == BrandingInfo.DefaultRigctldPort))
        {
            var rig = new DiscoveredRig($"localhost:{BrandingInfo.DefaultRigctldPort}", 0, "rigctld",
                                        2, "rigctld (NET rigctl)", 1.0, 1, UseRigctld: true);
            yield return new(ProbeEventKind.Found, rig.Port,
                Strings.Format("Disc_FoundRigctld", BrandingInfo.DefaultRigctldPort), Rig: rig);
        }

        if (listeners.Any(l => l.Port == 12345))
            yield return new(ProbeEventKind.Note, "localhost:12345", Strings.Get("Disc_FlrigSeen"));
    }

    // ── Stage 1: ranking ──────────────────────────────────────────────────
    // Family order: the family of the radio already chosen in the Connection
    // panel, then the family WSJT-X / JS8Call are configured for, then the
    // family the cable's VID/PID hints at, then by weight. Baud order is the
    // family's list, with the operator's chosen baud and any configured
    // baud moved to the front for their families — the common failure is
    // "right radio, wrong port or speed", so the configured values are the
    // best first guess and the configured port is probed first by the caller.

    private List<(RigFamily Family, int Baud)> RankCandidates(ComPortInfo port, DiscoveryRequest request)
    {
        var preferred = _data.FamilyForModel(request.PreferredModelId);
        var hinted    = _data.FamilyForVendor(port.RadioFamily);
        var clueFamilies = request.Clues
            .Select(c => (Family: _data.FamilyForModel(c.ModelId), c.Baud))
            .Where(x => x.Family is not null)
            .ToList();

        var families = _data.Families
            .OrderByDescending(f => f == preferred ? 3 : 0)
            .ThenByDescending(f => clueFamilies.Any(c => c.Family == f) ? 2 : 0)
            .ThenByDescending(f => f == hinted ? 1 : 0)
            .ThenByDescending(f => f.Weight)
            .ToList();

        var list = new List<(RigFamily, int)>();
        foreach (var family in families)
        {
            var first = new List<int>();
            if (family == preferred && request.PreferredBaud > 0) first.Add(request.PreferredBaud);
            first.AddRange(clueFamilies.Where(c => c.Family == family && c.Baud > 0).Select(c => c.Baud));

            foreach (var baud in first.Distinct().Concat(family.Bauds.Where(b => !first.Contains(b))))
                list.Add((family, baud));
        }
        return list;
    }

    // ── Stage 2: one family at one baud ───────────────────────────────────

    private async Task<(DiscoveredRig? Rig, List<ProbeEvent> Events)> ProbeFamilyAsync(
        SerialProbe probe, ComPortInfo port, RigFamily family, int baud,
        DiscoveryRequest request, CancellationToken ct)
    {
        var events = new List<ProbeEvent>();

        ProbeOperations.TryParse(family.Probe, out var op);
        var (_, parsed) = await ExchangeAsync(probe, port.PortName, op, events, ct);

        // CI-V: the first frame goes to the broadcast address, which rigs act
        // on but never answer. If only our own echo came back (Echo Back is
        // on and the speed is right) — or nothing at all, since Echo Back can
        // be off — address each known rig in turn. Addresses come from
        // rig_ids.json; the frame layout is fixed here.
        if (op == ProbeOperation.IcomReadId && parsed.Kind is not (ProbeReplyKind.Identity or ProbeReplyKind.Frequency))
        {
            if (parsed.Kind == ProbeReplyKind.Echo)
                events.Add(new(ProbeEventKind.Note, port.PortName, Strings.Format("Disc_Echo", baud)));

            foreach (var address in CivAddresses(family, request))
            {
                ct.ThrowIfCancellationRequested();
                (_, parsed) = await ExchangeAsync(probe, port.PortName, op, events, ct, address, AddressedTimeoutMs);
                if (parsed.Kind is ProbeReplyKind.Identity or ProbeReplyKind.Frequency) break;
            }
        }

        // Icom rigs that predate the read-ID command answer NG or nothing;
        // a plain read-frequency still proves the family and the baud.
        if (parsed.Kind is (ProbeReplyKind.None or ProbeReplyKind.Acknowledged or ProbeReplyKind.Echo)
            && family.FallbackProbe is not null
            && ProbeOperations.TryParse(family.FallbackProbe, out var fallback))
        {
            (_, parsed) = await ExchangeAsync(probe, port.PortName, fallback, events, ct);
        }

        var rig = parsed.Kind switch
        {
            ProbeReplyKind.Identity  => ResolveByIdentity(port, family, baud, parsed.Id!, request, events),
            ProbeReplyKind.Frequency => ResolveByFrequency(port, family, baud, parsed.FrequencyHz, request, events),
            ProbeReplyKind.Acknowledged => Note(events, port.PortName, Strings.Format("Disc_Acknowledged", family.Name, baud)),
            ProbeReplyKind.Garbage      => Note(events, port.PortName, Strings.Get("Disc_Garbage")),
            _ => null,
        };
        return (rig, events);
    }

    // Every CI-V address rig_ids.json knows for this family, the address of
    // the radio chosen in the Connection panel first.
    private IEnumerable<byte> CivAddresses(RigFamily family, DiscoveryRequest request)
    {
        var entries = _data.Ids.Where(i => i.Family.Equals(family.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        var ordered = entries
            .OrderByDescending(i => i.HamlibModelId == request.PreferredModelId)
            .ThenByDescending(i => request.Clues.Any(c => c.ModelId == i.HamlibModelId))
            .Select(i => i.Reply);

        var seen = new HashSet<byte>();
        foreach (var reply in ordered)
        {
            if (byte.TryParse(reply, System.Globalization.NumberStyles.HexNumber, null, out var address) && seen.Add(address))
                yield return address;
        }
    }

    private static async Task<(byte[] Reply, ProbeReply Parsed)> ExchangeAsync(
        SerialProbe probe, string portName, ProbeOperation op, List<ProbeEvent> events, CancellationToken ct,
        byte civAddress = 0x00, int timeoutMs = ReplyTimeoutMs)
    {
        var request = ProbeOperations.Bytes(op, civAddress);
        events.Add(new(ProbeEventKind.Sent, portName, op.ToString(), ProbeOperations.Dump(request)));

        var reply = await probe.ExchangeAsync(request, timeoutMs, QuietMs, ct);
        if (reply.Length > 0)
            events.Add(new(ProbeEventKind.Received, portName, string.Empty, ProbeOperations.Dump(reply)));
        else
            events.Add(new(ProbeEventKind.Note, portName, Strings.Get("Disc_NoReply")));

        Log.Debug("Probe {Port} {Op}: sent {Sent} got {Got}", portName, op,
            ProbeOperations.Dump(request), ProbeOperations.Dump(reply));
        return (reply, ProbeOperations.Parse(op, reply));
    }

    private static DiscoveredRig? Note(List<ProbeEvent> events, string port, string text)
    {
        events.Add(new(ProbeEventKind.Note, port, text));
        return null;
    }

    // ── Model resolution ──────────────────────────────────────────────────

    // The rig named itself. Several rigs may share an identity (Elecraft);
    // the radio chosen in the Connection panel breaks the tie, else the
    // first entry in rig_ids.json. An identity not in the table still
    // proves the family: fall back to the family's default model and say so.
    private DiscoveredRig ResolveByIdentity(ComPortInfo port, RigFamily family, int baud, string id,
                                            DiscoveryRequest request, List<ProbeEvent> events)
    {
        var matches = _data.Lookup(family.Id, id);
        if (matches.Count > 0)
        {
            var pick = matches.FirstOrDefault(m => m.HamlibModelId == request.PreferredModelId) ?? matches[0];
            if (matches.Count > 1)
                events.Add(new(ProbeEventKind.Note, port.PortName,
                    Strings.Format("Disc_SharedId", id, string.Join(", ", matches.Select(m => m.Name)), pick.Name)));
            return new(port.PortName, baud, family.Id, pick.HamlibModelId, pick.Name, 1.0, family.HandoffStopBits);
        }

        events.Add(new(ProbeEventKind.Note, port.PortName, Strings.Format("Disc_UnknownId", id, family.Name)));
        var (modelId, name) = DefaultModel(family, request);
        return new(port.PortName, baud, family.Id, modelId, name, 0.7, family.HandoffStopBits);
    }

    // A frequency came back. Inside a ham band is strong evidence; merely
    // plausible is weaker but still worth handing to rigctl to confirm.
    private DiscoveredRig? ResolveByFrequency(ComPortInfo port, RigFamily family, int baud, long hz,
                                              DiscoveryRequest request, List<ProbeEvent> events)
    {
        var score = ProbeOperations.IsInHamBand(hz) ? 0.8
                  : ProbeOperations.IsPlausibleFrequency(hz) ? 0.6
                  : 0.0;

        events.Add(new(ProbeEventKind.Response, port.PortName,
            Strings.Format("Disc_FrequencyReply", (hz / 1_000_000.0).ToString("0.000"), score >= 0.8 ? " " + Strings.Get("Disc_InBand") : string.Empty)));

        if (score == 0.0) return null;

        var (modelId, name) = DefaultModel(family, request);
        return new(port.PortName, baud, family.Id, modelId, name, score, family.HandoffStopBits);
    }

    // The family is known but the rig did not name itself: use the radio
    // chosen in the Connection panel when it belongs to this family, else
    // the family's default from rig_families.json.
    private (int ModelId, string Name) DefaultModel(RigFamily family, DiscoveryRequest request)
    {
        var modelId = _data.FamilyForModel(request.PreferredModelId) == family
            ? request.PreferredModelId
            : family.DefaultModelId;

        var name = _presets.Presets.FirstOrDefault(p => p.HamlibModelId == modelId)?.Name
                   ?? _data.Ids.FirstOrDefault(i => i.HamlibModelId == modelId)?.Name
                   ?? $"{family.Name} ({modelId})";
        return (modelId, name);
    }
}
