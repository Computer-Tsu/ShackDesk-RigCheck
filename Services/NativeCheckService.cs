using RigCheck.Localization;
using RigCheck.Models;
using Serilog;

namespace RigCheck.Services;

/// <summary>
/// The "Radio check": reads identity, frequency, mode, PTT state, and
/// S-meter straight over the serial port in the radio's own protocol,
/// without Hamlib. Runs before the Hamlib tests so the two together give
/// the diagnosis Hamlib alone cannot:
///
///   radio check passes, Hamlib fails → cable and radio are fine; the
///       problem is in Hamlib's or the program's configuration
///   radio check fails                → the radio is not answering at all
///
/// It also gives a PC with no Hamlib installed a real test. Same safety
/// rules as discovery: SerialProbe never raises RTS/DTR, and every byte
/// sent comes from NativeOperations' fixed table. Every exchange is
/// reported to the transcript so it can be replayed by hand.
/// </summary>
public class NativeCheckService
{
    private const int ReplyTimeoutMs = 400;
    private const int QuietMs        = 60;

    private readonly DiscoveryDataService _data;

    public NativeCheckService(DiscoveryDataService data) => _data = data;

    /// <summary>The family the selected model belongs to, when the native tier can speak it.</summary>
    public RigFamily? FamilyFor(ConnectionConfig cfg)
    {
        if (cfg.UseRigctld) return null;
        var family = _data.FamilyForModel(cfg.ModelId);
        return family is not null && NativeOperations.Supports(family.Id) ? family : null;
    }

    /// <summary>The checks that will run for a family, in order — for the pending placeholders.</summary>
    public static IReadOnlyList<TestId> Checks(RigFamily family) =>
        Ops.Where(o => NativeOperations.Bytes(family.Id, o.Op, 0) is not null).Select(o => o.Id).ToList();

    private static readonly (NativeOp Op, TestId Id)[] Ops =
    [
        (NativeOp.Identify,  TestId.NativeIdentify),
        (NativeOp.Frequency, TestId.NativeFrequency),
        (NativeOp.Mode,      TestId.NativeMode),
        (NativeOp.Ptt,       TestId.NativePtt),
        (NativeOp.Smeter,    TestId.NativeSmeter),
    ];

    public async Task<List<TestResult>> RunAsync(
        ConnectionConfig cfg, RigFamily family,
        IProgress<TestResult>? progress, Action<TranscriptKind, string>? transcript,
        CancellationToken ct)
    {
        var results = new List<TestResult>();
        var baud    = cfg.BaudRate > 0 ? cfg.BaudRate : family.Bauds.FirstOrDefault(9600);
        var address = CivAddressFor(cfg.ModelId);

        void Say(TranscriptKind kind, string text) => transcript?.Invoke(kind, text);
        void Add(TestResult r) { results.Add(r); progress?.Report(r); }

        Say(TranscriptKind.Command, Strings.Format("Native_Start", family.Name, cfg.ComPort, baud));

        using var probe = SerialProbe.TryOpen(cfg.ComPort, baud, out var failure);
        if (probe is null)
        {
            var reason = failure == "in use" ? Strings.Format("Disc_InUse", cfg.ComPort)
                                             : Strings.Format("Disc_OpenFailed", cfg.ComPort, failure ?? string.Empty);
            Say(TranscriptKind.Note, reason);
            foreach (var (_, id) in Ops.Where(o => NativeOperations.Bytes(family.Id, o.Op, address) is not null))
                Add(TestResult.Skipped(id, reason));
            return results;
        }

        foreach (var (op, id) in Ops)
        {
            ct.ThrowIfCancellationRequested();
            var request = NativeOperations.Bytes(family.Id, op, address);
            if (request is null) continue;

            var display = NativeOperations.Display(family.Id, op, address);
            Say(TranscriptKind.SerialTx, display);

            var reply = await probe.ExchangeAsync(request, ReplyTimeoutMs, QuietMs, ct);
            if (reply.Length > 0) Say(TranscriptKind.SerialRx, ProbeOperations.Dump(reply));
            else                  Say(TranscriptKind.Note, Strings.Get("Disc_NoReply"));

            var parsed = NativeOperations.Parse(family.Id, op, reply);
            Log.Debug("Native {Op} on {Port}: {Value} ok={Ok}", op, cfg.ComPort, parsed.Value, parsed.Ok);

            Add(parsed.Ok
                ? TestResult.Pass(id, Message(op, parsed, family, cfg), display)
                : reply.Length == 0
                    ? TestResult.Fail(id, Strings.Get("Native_NoReply"), display, NoReplyDiagnosis(cfg, baud, family), RigctlError.NoResponse)
                    : TestResult.Warning(id, Strings.Format("Native_Unparsed", ProbeOperations.Dump(reply)), display));

            // No point hammering a silent radio with four more queries.
            if (!parsed.Ok && reply.Length == 0 && op == NativeOp.Identify)
            {
                foreach (var (_, rest) in Ops.Skip(1).Where(o => NativeOperations.Bytes(family.Id, o.Op, address) is not null))
                    Add(TestResult.Skipped(rest, Strings.Get("Native_SkippedSilent")));
                break;
            }
        }

        return results;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // The CI-V address of the selected Icom model from rig_ids.json; other
    // families ignore it. Unknown Icom → 00 (broadcast), which will not
    // answer — the result says so and points at Find my radio.
    private byte CivAddressFor(int modelId)
    {
        var entry = _data.Ids.FirstOrDefault(i => i.HamlibModelId == modelId && i.Family == "Icom");
        return entry is not null && byte.TryParse(entry.Reply, System.Globalization.NumberStyles.HexNumber, null, out var a) ? a : (byte)0;
    }

    private string Message(NativeOp op, NativeReply parsed, RigFamily family, ConnectionConfig cfg) => op switch
    {
        NativeOp.Identify  => IdentityMessage(parsed, family, cfg),
        NativeOp.Frequency => Strings.Format("Native_Frequency", parsed.Value),
        NativeOp.Mode      => Strings.Format("Native_Mode", parsed.Value),
        NativeOp.Ptt       => parsed.Value == "TX" ? Strings.Get("Native_PttOn") : Strings.Get("Native_PttOff"),
        NativeOp.Smeter    => Strings.Format("Native_Smeter", parsed.Value),
        _ => parsed.Value,
    };

    // Name the radio from its identity where the table knows it; flag a
    // mismatch with the selected model, which is the classic wrong-preset case.
    private string IdentityMessage(NativeReply parsed, RigFamily family, ConnectionConfig cfg)
    {
        var matches = _data.Lookup(family.Id, parsed.Value);
        if (matches.Count == 0)
            return Strings.Format("Native_IdentityUnknown", parsed.Value);

        var selected = matches.FirstOrDefault(m => m.HamlibModelId == cfg.ModelId);
        return selected is not null
            ? Strings.Format("Native_IdentityMatch", selected.Name, parsed.Value)
            : Strings.Format("Native_IdentityMismatch", matches[0].Name, parsed.Value, cfg.RadioModelName);
    }

    private static DiagnosticResult NoReplyDiagnosis(ConnectionConfig cfg, int baud, RigFamily family) => new(
        Summary:      Strings.Format("Native_NoReply_Summary", cfg.ComPort, baud),
        Checks:
        [
            Strings.Get("Native_NoReply_1"),
            Strings.Format("Native_NoReply_2", baud),
            Strings.Get("Native_NoReply_3"),
            family.Id == "Icom" ? Strings.Get("Native_NoReply_Icom") : Strings.Get("Native_NoReply_4"),
        ],
        FixCommand:   null,
        LearnMoreUrl: null);
}
