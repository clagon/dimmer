using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Dimmer.Overlay;

/// <summary>マウスカーソルが乗っている Monitor。</summary>
public static class CursorMonitor
{
    /// <summary>今カーソルがある Monitor の HMONITOR。</summary>
    public static nint Handle()
    {
        PInvoke.GetCursorPos(out var p);
        return PInvoke.MonitorFromPoint(p, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
    }
}
