using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Dimmer.Overlay;

/// <summary>マウスカーソルが乗っている Monitor と、GDI デバイス名と HMONITOR の対応。</summary>
public static class CursorMonitor
{
    /// <summary>今カーソルがある Monitor の HMONITOR。</summary>
    public static nint Handle()
    {
        PInvoke.GetCursorPos(out var p);
        return PInvoke.MonitorFromPoint(p, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
    }

    /// <summary>今カーソルがある Monitor の GDI デバイス名（<c>\\.\DISPLAY1</c>）。</summary>
    public static string GdiName() => GdiNameOf(Handle());

    /// <summary>GDI デバイス名の Monitor の HMONITOR。見つからなければ 0。</summary>
    public static unsafe nint HandleOf(string gdiName)
    {
        nint found = 0;
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (h, _, _, _) =>
        {
            if (GdiNameOf(h) != gdiName) return true;
            found = h;
            return false;
        }, 0);
        return found;
    }

    private static unsafe string GdiNameOf(nint hmonitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (!PInvoke.GetMonitorInfo(new HMONITOR(hmonitor), (MONITORINFO*)&info)) return "";
        var s = info.szDevice.AsReadOnlySpan();
        return (s.IndexOf('\0') is var i and >= 0 ? s[..i] : s).ToString();
    }
}
