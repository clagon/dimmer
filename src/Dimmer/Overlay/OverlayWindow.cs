using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Dimmer.Overlay;

/// <summary>
/// Shortcut の実行結果を、Monitor の下中央（タスクバーのすぐ上）に約 1.5 秒出す小さな表示。
/// フォーカスを奪わず、クリックは下のウィンドウに素通りさせ、常に最前面に出す。UI スレッドで使うこと。
/// </summary>
public sealed class OverlayWindow
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(1.5);
    private const int WidthDip = 320, HeightDip = 76, BottomMarginDip = 24;

    private readonly Window _window = new();
    private readonly TextBlock _title = new() { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _detail = new() { FontSize = 13, Opacity = 0.8, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100 };
    private readonly DispatcherQueueTimer _hideTimer;
    private bool _visible;

    public OverlayWindow()
    {
        var text = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_title);
        text.Children.Add(_detail);
        text.Children.Add(_bar);
        _window.Content = new Border
        {
            Padding = new Thickness(18, 10, 18, 10),
            Background = new SolidColorBrush(ColorHelper.FromArgb(0xF2, 0x20, 0x20, 0x20)),
            RequestedTheme = ElementTheme.Dark,
            Child = text,
        };

        var appWindow = _window.AppWindow;
        appWindow.IsShownInSwitchers = false;
        if (appWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.IsAlwaysOnTop = true;
        }

        // フォーカスを奪わない（NOACTIVATE）、クリックを素通りさせる（LAYERED + TRANSPARENT）、タスクバーに出さない（TOOLWINDOW）
        var hwnd = new HWND(Microsoft.UI.Win32Interop.GetWindowFromWindowId(appWindow.Id));
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)
            | WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_LAYERED
            | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
        PInvoke.SetLayeredWindowAttributes(hwnd, new COLORREF(0), 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

        _hideTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _hideTimer.IsRepeating = false;
        _hideTimer.Interval = Duration;
        _hideTimer.Tick += (_, _) => { _window.AppWindow.Hide(); _visible = false; };
    }

    /// <summary>
    /// 表示する。表示中なら出し直さずに中身を更新し、表示時間を延ばす。
    /// </summary>
    /// <param name="title">1 行目（Profile 名や「Monitor Name の Setting」）</param>
    /// <param name="detail">2 行目（位置や値、エラー）。無ければ出さない</param>
    /// <param name="percent">バーに出す値。無ければバーを出さない</param>
    /// <param name="monitor">表示する Monitor の HMONITOR。0 なら Cursor Monitor</param>
    public void Show(string title, string? detail = null, int? percent = null, nint monitor = 0)
    {
        _title.Text = title;
        _detail.Text = detail ?? "";
        _detail.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        _bar.Visibility = percent is null ? Visibility.Collapsed : Visibility.Visible;
        if (percent is { } v) _bar.Value = v;

        _window.AppWindow.MoveAndResize(BottomCenterOf(monitor == 0 ? CursorMonitor.Handle() : monitor));
        if (!_visible)
        {
            _window.AppWindow.Show(activateWindow: false);
            _visible = true;
        }
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private static unsafe RectInt32 BottomCenterOf(nint hmonitor)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(new HMONITOR(hmonitor), &info);
        var scale = PInvoke.GetDpiForMonitor(new HMONITOR(hmonitor), MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _).Succeeded ? dpi / 96.0 : 1.0;

        // 作業領域（タスクバーを除いた範囲）の下中央
        var work = info.rcWork;
        var w = (int)(WidthDip * scale);
        var h = (int)(HeightDip * scale);
        return new RectInt32(work.left + (work.right - work.left - w) / 2, work.bottom - h - (int)(BottomMarginDip * scale), w, h);
    }
}
