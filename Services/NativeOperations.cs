using System.Text;

namespace RigCheck.Services;

// ── Native (no-Hamlib) read operations ───────────────────────────────────
// The second fixed, compiled allowlist, alongside ProbeOperations: what the
// direct "Radio check" may send to a serial port. Every entry is a read.
// There is deliberately no PTT command here at all — PTT state is read
// from a status query, never by sending a transmit command:
//   Kenwood "TX;" SETS transmit, so it is never used; "IF;" carries the TX flag.
//   Icom  1C 00 reads PTT; 1C 00 01 would set it and is not in the table.
// A family that lacks an operation returns null, and the check is skipped.

public enum NativeOp { Identify, Frequency, Mode, Ptt, Smeter }

/// <summary>A decoded reply: Ok with a human value, or not Ok with why.</summary>
public record NativeReply(bool Ok, string Value = "", long Number = 0, bool Garbage = false);

public static class NativeOperations
{
    private const byte CivController = 0xE0;

    /// <summary>Families the native tier can speak. Others fall back to Hamlib only.</summary>
    public static bool Supports(string familyId) => familyId is "Icom" or "Kenwood" or "YaesuCat";

    /// <summary>Bytes to send, or null when the family has no read for that operation.</summary>
    public static byte[]? Bytes(string familyId, NativeOp op, byte civAddress) => familyId switch
    {
        "Icom" => op switch
        {
            NativeOp.Identify  => Civ(civAddress, 0x19, 0x00),
            NativeOp.Frequency => Civ(civAddress, 0x03),
            NativeOp.Mode      => Civ(civAddress, 0x04),
            NativeOp.Ptt       => Civ(civAddress, 0x1C, 0x00),
            NativeOp.Smeter    => Civ(civAddress, 0x15, 0x02),
            _ => null,
        },
        "Kenwood" or "YaesuCat" => op switch
        {
            NativeOp.Identify  => "ID;"u8.ToArray(),
            NativeOp.Frequency => "FA;"u8.ToArray(),
            NativeOp.Mode      => familyId == "YaesuCat" ? "MD0;"u8.ToArray() : "MD;"u8.ToArray(),
            // Kenwood/Elecraft: the IF status query carries the TX flag. Yaesu's IF
            // does not, and its read form "TX;" is the SET command on a Kenwood —
            // if the operator picked the wrong model that would key the rig. So no
            // PTT read for Yaesu CAT in the native tier; Hamlib covers it.
            NativeOp.Ptt       => familyId == "YaesuCat" ? null : (byte[]?)"IF;"u8.ToArray(),
            NativeOp.Smeter    => "SM0;"u8.ToArray(),
            _ => null,
        },
        _ => null,
    };

    /// <summary>The bytes as an operator would type them into a terminal, for the transcript.</summary>
    public static string Display(string familyId, NativeOp op, byte civAddress)
    {
        var bytes = Bytes(familyId, op, civAddress);
        return bytes is null ? string.Empty : ProbeOperations.Dump(bytes);
    }

    public static NativeReply Parse(string familyId, NativeOp op, byte[] reply)
    {
        if (reply.Length == 0) return new(false);
        return familyId switch
        {
            "Icom"                  => ParseCiv(op, reply),
            "Kenwood" or "YaesuCat" => ParseAscii(familyId, op, reply),
            _                       => new(false),
        };
    }

    // ── Icom CI-V ─────────────────────────────────────────────────────────

    private static byte[] Civ(byte address, params byte[] command) =>
        [0xFE, 0xFE, address, CivController, ..command, 0xFD];

    private static NativeReply ParseCiv(NativeOp op, byte[] reply)
    {
        // Skip our own echo (from == controller); take the first answer addressed to us.
        var frame = Frames(reply).FirstOrDefault(f => f.Length >= 6 && f[3] != CivController && f[2] == CivController);
        if (frame is null) return new(false, Garbage: Frames(reply).Any());

        var cmd = frame[4];
        if (cmd == 0xFA) return new(false, "NG");
        var data = frame[5..^1];   // after cmd byte, before FD

        return op switch
        {
            NativeOp.Identify when cmd == 0x19 && data.Length >= 2 =>
                new(true, data[1].ToString("X2"), data[1]),

            NativeOp.Frequency when cmd == 0x03 && data.Length >= 5 =>
                Freq(BcdLittleEndian(data, 0, 5)),

            NativeOp.Mode when cmd == 0x04 && data.Length >= 1 =>
                new(true, IcomMode(data[0]), data[0]),

            NativeOp.Ptt when cmd == 0x1C && data.Length >= 2 =>
                new(true, data[1] == 0 ? "RX" : "TX", data[1]),

            // 0000 = S0, 0120 = S9, 0241 = S9+60 dB on Icom's scale
            NativeOp.Smeter when cmd == 0x15 && data.Length >= 3 =>
                IcomSmeter((int)BcdBigEndian(data, 1, 2)),

            _ => new(false, Garbage: true),
        };
    }

    private static IEnumerable<byte[]> Frames(byte[] buf)
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

    private static long BcdLittleEndian(byte[] b, int start, int count)
    {
        long v = 0;
        for (int i = start + count - 1; i >= start; i--) v = v * 100 + (b[i] >> 4) * 10 + (b[i] & 0x0F);
        return v;
    }

    private static long BcdBigEndian(byte[] b, int start, int count)
    {
        long v = 0;
        for (int i = start; i < start + count; i++) v = v * 100 + (b[i] >> 4) * 10 + (b[i] & 0x0F);
        return v;
    }

    private static string IcomMode(byte code) => code switch
    {
        0x00 => "LSB", 0x01 => "USB", 0x02 => "AM", 0x03 => "CW", 0x04 => "RTTY",
        0x05 => "FM", 0x06 => "WFM", 0x07 => "CW-R", 0x08 => "RTTY-R", 0x17 => "DV",
        _ => $"mode {code:X2}",
    };

    private static NativeReply IcomSmeter(int raw)
    {
        // Linear to S9 at 120, then 60 dB over the remaining 121 counts.
        string label = raw <= 120
            ? $"S{Math.Round(raw / (120.0 / 9))}"
            : $"S9+{Math.Round((raw - 120) * 60.0 / 121)}";
        return new(true, label, raw);
    }

    // ── Kenwood / Elecraft / Yaesu CAT (ASCII, ';'-terminated) ────────────

    private static NativeReply ParseAscii(string familyId, NativeOp op, byte[] reply)
    {
        var text = Encoding.ASCII.GetString(reply).Trim();
        if (text.Contains("?;", StringComparison.Ordinal) && !text.Contains("ID", StringComparison.Ordinal))
            return new(false, "?;");

        return op switch
        {
            NativeOp.Identify  => Field(text, "ID", v => new(true, "ID" + v + ";")),
            NativeOp.Frequency => Field(text, "FA", v => long.TryParse(v, out var hz) ? Freq(hz) : new(false, Garbage: true)),
            NativeOp.Mode      => Field(text, familyId == "YaesuCat" ? "MD0" : "MD",
                                        v => new(true, AsciiMode(familyId, v), 0)),
            // Kenwood IF: P1 freq(11) P2 step(5) P3 RIT(6) P4 P5 P6 (1 each) P7 channel(2)
            // then P8 = TX flag, i.e. index 27 of the payload after "IF".
            NativeOp.Ptt       => Field(text, "IF", v => v.Length >= 28
                                        ? new(true, v[27] == '1' ? "TX" : "RX", v[27] == '1' ? 1 : 0)
                                        : new(false, Garbage: true)),
            NativeOp.Smeter    => Field(text, "SM0", v => int.TryParse(v, out var n)
                                        ? new(true, familyId == "YaesuCat" ? $"{n}/255" : $"{n}/30", n)
                                        : new(false, Garbage: true)),
            _ => new(false),
        };
    }

    // Find "PREFIX…;" in the reply and hand the middle to the parser.
    private static NativeReply Field(string text, string prefix, Func<string, NativeReply> parse)
    {
        var at = text.IndexOf(prefix, StringComparison.Ordinal);
        if (at < 0) return new(false, Garbage: true);
        var end = text.IndexOf(';', at);
        if (end < 0) return new(false, Garbage: true);
        return parse(text[(at + prefix.Length)..end]);
    }

    private static string AsciiMode(string familyId, string v) =>
        (familyId, v) switch
        {
            (_, "1") => "LSB", (_, "2") => "USB", (_, "3") => "CW",
            (_, "4") => "FM",  (_, "5") => "AM",  (_, "6") => familyId == "YaesuCat" ? "RTTY" : "FSK",
            (_, "7") => "CW-R", (_, "9") => familyId == "YaesuCat" ? "RTTY-R" : "FSK-R",
            ("YaesuCat", "8") => "DATA-LSB", ("YaesuCat", "C") => "DATA-USB",
            _ => $"mode {v}",
        };

    private static NativeReply Freq(long hz) =>
        new(true, (hz / 1_000_000.0).ToString("0.000000") + " MHz", hz);
}
