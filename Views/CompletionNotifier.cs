using RigCheck.Services;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RigCheck.Views;

/// <summary>
/// Tells the operator a run has finished when they have switched away to
/// WSJT-X or a browser: flashes the taskbar button until the window is
/// brought to the front, and optionally plays a system sound.
/// Both are settings; see RigCheckSettings.NotifyFlash / NotifySound.
/// </summary>
public static class CompletionNotifier
{
    public static void Notify(Window window, RigCheckSettings settings, bool success)
    {
        if (settings.NotifySound)
        {
            // System sounds respect the user's Windows sound scheme (and mute).
            if (success) SystemSounds.Asterisk.Play();
            else         SystemSounds.Exclamation.Play();
        }

        // Flashing the active window is just a distraction; only do it when
        // the operator is looking at something else.
        if (settings.NotifyFlash && !window.IsActive)
            FlashUntilForeground(window);
    }

    // ── Win32 FlashWindowEx ───────────────────────────────────────────────
    // WPF has no managed equivalent. FLASHW_TIMERNOFG keeps flashing until
    // the window comes to the foreground, then Windows stops it for us.

    private const uint FLASHW_ALL       = 0x3;
    private const uint FLASHW_TIMERNOFG = 0xC;

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint   cbSize;
        public IntPtr hwnd;
        public uint   dwFlags;
        public uint   uCount;
        public uint   dwTimeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private static void FlashUntilForeground(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var info = new FLASHWINFO
        {
            cbSize    = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd      = hwnd,
            dwFlags   = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount    = 0,
            dwTimeout = 0,
        };
        FlashWindowEx(ref info);
    }
}
