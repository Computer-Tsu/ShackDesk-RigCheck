using Serilog;
using System.IO;
using System.IO.Ports;

namespace RigCheck.Services;

/// <summary>
/// One open serial port during discovery. Keeps the handle across the
/// baud sweep (opening a USB-serial port is slow and some adapters reset
/// DTR/RTS on open) and does a single write-then-collect exchange.
///
/// RTS and DTR are never asserted. On a great many interfaces RTS or DTR
/// IS the PTT line — asserting either would key the transmitter. That rule
/// does not have a setting.
/// </summary>
public sealed class SerialProbe : IDisposable
{
    private readonly SerialPort _port;

    public string PortName => _port.PortName;

    private SerialProbe(SerialPort port) => _port = port;

    /// <summary>
    /// Open the port at the first baud rate. Returns null when the port is
    /// owned by another program (the common, informative case) or missing.
    /// </summary>
    public static SerialProbe? TryOpen(string portName, int baud, out string? failure)
    {
        failure = null;
        var port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
        {
            Handshake    = Handshake.None,
            DtrEnable    = false,
            RtsEnable    = false,
            ReadTimeout  = 50,
            WriteTimeout = 300,
        };

        try
        {
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            return new SerialProbe(port);
        }
        catch (UnauthorizedAccessException)
        {
            failure = "in use";
            port.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            failure = ex.Message;
            Log.Debug(ex, "Could not open {Port}", portName);
            port.Dispose();
            return null;
        }
    }

    /// <summary>Change speed without closing; settle briefly so the adapter is ready.</summary>
    public async Task SetBaudAsync(int baud, CancellationToken ct)
    {
        if (_port.BaudRate == baud) return;
        _port.BaudRate = baud;
        _port.DiscardInBuffer();
        await Task.Delay(30, ct);
    }

    /// <summary>
    /// Write the bytes, then collect whatever arrives until the line has
    /// been quiet for <paramref name="quietMs"/> or <paramref name="timeoutMs"/>
    /// has passed. Runs on a worker thread; SerialPort reads are blocking.
    /// </summary>
    public Task<byte[]> ExchangeAsync(byte[] request, int timeoutMs, int quietMs, CancellationToken ct) =>
        Task.Run(() =>
        {
            _port.DiscardInBuffer();
            _port.Write(request, 0, request.Length);

            var collected = new MemoryStream();
            var buffer    = new byte[256];
            var deadline  = Environment.TickCount64 + timeoutMs;
            var lastByte  = Environment.TickCount64;
            var gotAny    = false;

            while (Environment.TickCount64 < deadline && !ct.IsCancellationRequested)
            {
                try
                {
                    var n = _port.Read(buffer, 0, buffer.Length);   // blocks up to ReadTimeout (50 ms)
                    if (n > 0)
                    {
                        collected.Write(buffer, 0, n);
                        lastByte = Environment.TickCount64;
                        gotAny   = true;
                    }
                }
                catch (TimeoutException)
                {
                    // Nothing this tick. If we already have data and the line
                    // has gone quiet, the reply is complete.
                    if (gotAny && Environment.TickCount64 - lastByte >= quietMs) break;
                }
            }

            return collected.ToArray();
        }, ct);

    public void Dispose()
    {
        try { if (_port.IsOpen) _port.Close(); } catch { /* closing a yanked USB port throws; nothing to do */ }
        _port.Dispose();
    }
}
