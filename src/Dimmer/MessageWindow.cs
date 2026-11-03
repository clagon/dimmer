using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Dimmer;

/// <summary>
/// ウィンドウメッセージ（WM_DISPLAYCHANGE など）を受けるための見えないトップレベルウィンドウ。
/// ブロードキャストを受けるためメッセージ専用ウィンドウにはしない。UI スレッドで作ること。
/// </summary>
public sealed unsafe class MessageWindow : IDisposable
{
    private const string ClassName = "Dimmer.MessageWindow";
    private static readonly WNDPROC Proc = WndProc;
    private static MessageWindow? _instance;

    public MessageWindow()
    {
        fixed (char* name = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = Proc,
                lpszClassName = name,
            };
            PInvoke.RegisterClassEx(wc);
            _hwnd = PInvoke.CreateWindowEx(0, ClassName, ClassName, WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0, HWND.Null, null, null, null);
        }
        _instance = this;
    }

    private readonly HWND _hwnd;

    public nint Handle => _hwnd;

    /// <summary>受け取ったメッセージ（msg, wParam, lParam）。UI スレッドで呼ばれる。</summary>
    public event Action<uint, nuint, nint>? MessageReceived;

    public void Dispose() => PInvoke.DestroyWindow(_hwnd);

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        _instance?.MessageReceived?.Invoke(msg, wParam.Value, lParam.Value);
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
