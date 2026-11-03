using Dimmer.Core;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Dimmer.Shortcuts;

/// <summary>
/// RegisterHotKey でグローバルな Shortcut を登録し、押された・離されたを知らせる。UI スレッドで作り、UI スレッドで使うこと。
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private static readonly TimeSpan ReleasePollInterval = TimeSpan.FromMilliseconds(20);

    private readonly HWND _hwnd;
    private readonly Dictionary<int, (Shortcut Shortcut, bool Repeat)> _registered = [];
    private readonly HashSet<int> _held = [];
    private readonly DispatcherQueueTimer _releasePoll;
    private int _nextId = 1;

    public HotkeyService(MessageWindow window)
    {
        _hwnd = new HWND(window.Handle);
        window.MessageReceived += OnMessage;
        _releasePoll = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _releasePoll.Interval = ReleasePollInterval;
        _releasePoll.Tick += (_, _) => PollReleased();
    }

    /// <summary>
    /// 押されたとき。第 2 引数はキーリピートによる 2 回目以降の通知か（repeat を指定して登録したものだけが true になりうる）。
    /// </summary>
    public event Action<int, bool>? Pressed;

    /// <summary>キーが離されたとき（GetAsyncKeyState で検出する）。</summary>
    public event Action<int>? Released;

    /// <summary>登録する。成功すれば ID、他のアプリとの衝突や Windows の予約で失敗すれば null。</summary>
    /// <param name="repeat">
    /// キーリピートの通知も受け取るか。受け取らない（既定）なら MOD_NOREPEAT を付け、届く通知はすべて新しく押されたものになる
    /// （離したことを検出する前に押し直されても、取りこぼさない）。
    /// </param>
    public int? Register(Shortcut shortcut, bool repeat = false)
    {
        var id = _nextId++;
        var modifiers = (HOT_KEY_MODIFIERS)shortcut.Modifiers | (repeat ? 0 : HOT_KEY_MODIFIERS.MOD_NOREPEAT);
        if (!PInvoke.RegisterHotKey(_hwnd, id, modifiers, (uint)shortcut.Key)) return null;
        _registered[id] = (shortcut, repeat);
        return id;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.Keys) PInvoke.UnregisterHotKey(_hwnd, id);
        _registered.Clear();
        _held.Clear();
        _releasePoll.Stop();
    }

    public void Dispose() => UnregisterAll();

    private void OnMessage(uint msg, nuint wParam, nint lParam)
    {
        if (msg != PInvoke.WM_HOTKEY || !_registered.TryGetValue((int)wParam, out var r)) return;
        var id = (int)wParam;
        // MOD_NOREPEAT で登録したものは、押しっぱなしの間に届くことはないので常に新しく押されたもの
        var isRepeat = !_held.Add(id) && r.Repeat;
        if (!isRepeat) _releasePoll.Start();
        Pressed?.Invoke(id, isRepeat);
    }

    private void PollReleased()
    {
        foreach (var id in _held.ToList())
        {
            // 修飾キーではなく、主となるキーが離されたかで判定する
            if (_registered.TryGetValue(id, out var r) && (PInvoke.GetAsyncKeyState(r.Shortcut.Key) & 0x8000) != 0) continue;
            _held.Remove(id);
            Released?.Invoke(id);
        }
        if (_held.Count == 0) _releasePoll.Stop();
    }
}
