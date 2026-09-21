using System.Text;

namespace RigCheck.Services;

// ── Probe operations ──────────────────────────────────────────────────────
// The complete, fixed set of things Find my radio can send to a serial
// port. Every one is a read: identify yourself, or what frequency are you
// on. rig_families.json picks an operation BY NAME; it can never supply
// bytes of its own, so a tampered or auto-updated data file cannot make
// RigCheck send anything else at a transmitter.
//
// Adding an operation means adding it here, in code, with a review.

public enum ProbeOperation
{
    /// <summary>"ID;" — Kenwood, Elecraft, and Yaesu CAT rigs answer "IDnnn;".</summary>
    KenwoodId,
    /// <summary>CI-V read transceiver ID (cmd 19 00) to the broadcast address; the rig answers with its own address.</summary>
    IcomReadId,
    /// <summary>CI-V read operating frequency (cmd 03). Fallback for rigs that do not answer 19 00.</summary>
    IcomReadFreq,
    /// <summary>Yaesu FT-817/857/897 five-byte read frequency (opcode 03).</summary>
    YaesuLegacyReadFreq,
}

public enum ProbeReplyKind
{
    /// <summary>Nothing came back in time.</summary>
    None,
    /// <summary>Bytes came back but nothing recognisable — often the wrong baud rate.</summary>
    Garbage,
    /// <summary>Only our own frame came back: a CI-V rig with Echo Back on is listening at this speed but was not addressed.</summary>
    Echo,
    /// <summary>The rig answered in the family's protocol but did not name itself (e.g. Kenwood "?;", CI-V NG).</summary>
    Acknowledged,
    /// <summary>The rig named itself; Id holds the identity string as it appears in rig_ids.json.</summary>
    Identity,
    /// <summary>The rig reported its frequency; FrequencyHz is set.</summary>
    Frequency,
}

public record ProbeReply(ProbeReplyKind Kind, string? Id = null, long FrequencyHz = 0);

public static class ProbeOperations
{
    // CI-V controller address rigctl and most software use for the PC.
    private const byte CivController = 0xE0;
    private const byte CivBroadcast  = 0x00;

    public static bool TryParse(string name, out ProbeOperation op) =>
        Enum.TryParse(name, ignoreCase: true, out op);

    /// <summary>
    /// The exact bytes sent for an operation. For CI-V, <paramref name="civAddress"/>
    /// is the rig address the frame is sent to. 00 is broadcast: rigs act on
    /// it but by design never answer, so identification has to address the
    /// rig directly — the addresses come from rig_ids.json.
    /// </summary>
    public static byte[] Bytes(ProbeOperation op, byte civAddress = CivBroadcast) => op switch
    {
        ProbeOperation.KenwoodId           => "ID;"u8.ToArray(),
        ProbeOperation.IcomReadId          => [0xFE, 0xFE, civAddress, CivController, 0x19, 0x00, 0xFD],
        ProbeOperation.IcomReadFreq        => [0xFE, 0xFE, civAddress, CivController, 0x03, 0xFD],
        ProbeOperation.YaesuLegacyReadFreq => [0x00, 0x00, 0x00, 0x00, 0x03],
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    /// <summary>Interpret whatever came back after sending an operation.</summary>
    public static ProbeReply Parse(ProbeOperation op, byte[] reply)
    {
        if (reply.Length == 0) return new(ProbeReplyKind.None);

        return op switch
        {
            ProbeOperation.KenwoodId           => ParseKenwood(reply),
            ProbeOperation.IcomReadId          => ParseCiv(reply, expectCmd: 0x19),
            ProbeOperation.IcomReadFreq        => ParseCiv(reply, expectCmd: 0x03),
            ProbeOperation.YaesuLegacyReadFreq => ParseYaesuLegacy(reply),
            _ => new(ProbeReplyKind.Garbage),
        };
    }

    // ── Kenwood-style ASCII ("ID023;") ────────────────────────────────────

    private static ProbeReply ParseKenwood(byte[] reply)
    {
        var text = Encoding.ASCII.GetString(reply);

        // "IDnnn;" anywhere in the buffer — some rigs prefix an echo or a stray ";".
        var at = text.IndexOf("ID", StringComparison.Ordinal);
        if (at >= 0)
        {
            var end = text.IndexOf(';', at);
            if (end > at + 2 && text.AsSpan(at + 2, end - at - 2).ToString().All(char.IsDigit))
                return new(ProbeReplyKind.Identity, text.Substring(at, end - at + 1));
        }

        // "?;" is Kenwood for "I heard you but did not understand" — right baud, right family.
        if (text.Contains("?;", StringComparison.Ordinal))
            return new(ProbeReplyKind.Acknowledged);

        return new(ProbeReplyKind.Garbage);
    }

    // ── Icom CI-V frames ──────────────────────────────────────────────────
    // Frame: FE FE <to> <from> <cmd> [<sub>] [data…] FD
    // The rig echoes our own frame first when CI-V transceive is on, so
    // frames addressed FROM the controller are skipped.

    private static ProbeReply ParseCiv(byte[] reply, byte expectCmd)
    {
        foreach (var frame in CivFrames(reply))
        {
            if (frame.Length < 5) continue;
            var to = frame[2]; var src = frame[3]; var cmd = frame[4];
            if (src == CivController) continue;                  // our own echo
            if (to != CivController && to != CivBroadcast) continue;

            if (cmd == 0xFA) return new(ProbeReplyKind.Acknowledged);   // NG — understood, refused
            if (cmd != expectCmd) continue;

            if (expectCmd == 0x19 && frame.Length >= 7)
                return new(ProbeReplyKind.Identity, frame[6].ToString("X2"));

            if (expectCmd == 0x03 && frame.Length >= 10)
                return new(ProbeReplyKind.Frequency, FrequencyHz: BcdLittleEndian(frame, 5, 5));
        }

        // Any well-formed frame from a non-controller address means CI-V at
        // this baud. Only our own frame back means Echo Back is on and the rig
        // is listening at this speed — it just was not addressed.
        var frames = CivFrames(reply).Where(f => f.Length >= 5).ToList();
        if (frames.Any(f => f[3] != CivController)) return new(ProbeReplyKind.Acknowledged);
        if (frames.Count > 0)                        return new(ProbeReplyKind.Echo);
        return new(ProbeReplyKind.Garbage);
    }

    private static IEnumerable<byte[]> CivFrames(byte[] buf)
    {
        int i = 0;
        while (i < buf.Length - 1)
        {
            if (buf[i] != 0xFE || buf[i + 1] != 0xFE) { i++; continue; }
            var end = Array.IndexOf(buf, (byte)0xFD, i + 2);
            if (end < 0) yield break;
            yield return buf[i..(end + 1)];
            i = end + 1;
        }
    }

    // CI-V frequency: 5 BCD bytes, least significant first (10 Hz digit pairs up).
    private static long BcdLittleEndian(byte[] buf, int start, int count)
    {
        long value = 0;
        for (int i = start + count - 1; i >= start; i--)
            value = value * 100 + ((buf[i] >> 4) * 10) + (buf[i] & 0x0F);
        return value;
    }

    // ── Yaesu five-byte (FT-817 family) ──────────────────────────────────
    // Reply to read-frequency: 4 BCD bytes, most significant first, in
    // 10 Hz units, then one mode byte.

    private static ProbeReply ParseYaesuLegacy(byte[] reply)
    {
        if (reply.Length < 5) return new(ProbeReplyKind.Garbage);

        long value = 0;
        for (int i = 0; i < 4; i++)
        {
            var hi = reply[i] >> 4; var lo = reply[i] & 0x0F;
            if (hi > 9 || lo > 9) return new(ProbeReplyKind.Garbage);
            value = value * 100 + hi * 10 + lo;
        }
        return new(ProbeReplyKind.Frequency, FrequencyHz: value * 10);
    }

    // ── Sanity check ──────────────────────────────────────────────────────

    /// <summary>
    /// True when a frequency sits inside an amateur band. A rig parked on a
    /// ham frequency is strong evidence that the reply is real and not a
    /// coincidence of bytes at the wrong baud rate.
    /// </summary>
    public static bool IsInHamBand(long hz)
    {
        var mhz = hz / 1_000_000.0;
        return HamBands.Any(b => mhz >= b.Low && mhz <= b.High);
    }

    /// <summary>Anything a real receiver could be tuned to — weaker evidence than a ham band.</summary>
    public static bool IsPlausibleFrequency(long hz) => hz is >= 100_000 and <= 3_000_000_000;

    private static readonly (double Low, double High)[] HamBands =
    [
        (1.8, 2.0), (3.5, 4.0), (5.25, 5.45), (7.0, 7.3), (10.1, 10.15), (14.0, 14.35),
        (18.068, 18.168), (21.0, 21.45), (24.89, 24.99), (28.0, 29.7), (50.0, 54.0),
        (70.0, 70.5), (144.0, 148.0), (222.0, 225.0), (420.0, 450.0), (902.0, 928.0),
        (1240.0, 1300.0), (2300.0, 2450.0),
    ];

    /// <summary>Hex dump with printable ASCII beside it, for the transcript.</summary>
    public static string Dump(byte[] bytes)
    {
        if (bytes.Length == 0) return string.Empty;
        var hex   = string.Join(" ", bytes.Select(b => b.ToString("X2")));
        var ascii = new string(bytes.Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.').ToArray());
        return ascii.Any(c => c != '.') ? $"{hex}   \"{ascii}\"" : hex;
    }
}
