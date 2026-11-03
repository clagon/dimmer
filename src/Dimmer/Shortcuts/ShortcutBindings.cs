using Dimmer.Core;

namespace Dimmer.Shortcuts;

/// <summary>Shortcut を割り当てる対象（Profile・Profile Cycle・Adjustment）。</summary>
/// <param name="Name">エラー表示に使う対象名</param>
/// <param name="Shortcut">設定ファイルに書かれた文字列（例: Ctrl+Alt+B）</param>
/// <param name="OnPressed">押されたとき。引数はキーリピートによる通知か</param>
/// <param name="OnReleased">離されたとき</param>
/// <param name="Repeat">キーリピートの通知も受け取るか（受け取らなければ、押すたびに 1 回だけ通知される）</param>
public sealed record ShortcutTarget(string Name, string Shortcut, Action<bool> OnPressed, Action? OnReleased = null, bool Repeat = false);

/// <summary>登録に失敗した Shortcut（設定画面で警告を出すために保持する）。</summary>
public sealed record FailedShortcut(string TargetName, string Shortcut, string Reason);

/// <summary>対象ごとの Shortcut をまとめて登録し、押されたら対象を実行する。</summary>
public sealed class ShortcutBindings
{
    private readonly HotkeyService _hotkeys;
    private readonly Dictionary<int, ShortcutTarget> _targets = [];

    public ShortcutBindings(HotkeyService hotkeys)
    {
        _hotkeys = hotkeys;
        _hotkeys.Pressed += (id, isRepeat) => { if (_targets.TryGetValue(id, out var t)) t.OnPressed(isRepeat); };
        _hotkeys.Released += id => { if (_targets.TryGetValue(id, out var t)) t.OnReleased?.Invoke(); };
    }

    /// <summary>登録に失敗した Shortcut の一覧。</summary>
    public IReadOnlyList<FailedShortcut> Failed { get; private set; } = [];

    /// <summary>
    /// 登録し直す。読めない・修飾キーが無い・アプリ内で重複している Shortcut は登録せず、
    /// 他のアプリとの衝突や Windows の予約で登録できなかったものと合わせて <see cref="Failed"/> に入れる。
    /// </summary>
    public IReadOnlyList<FailedShortcut> Bind(IEnumerable<ShortcutTarget> targets)
    {
        _hotkeys.UnregisterAll();
        _targets.Clear();

        var failed = new List<FailedShortcut>();
        var parsed = new List<(ShortcutTarget Target, Shortcut Shortcut)>();
        foreach (var t in targets.Where(t => !string.IsNullOrWhiteSpace(t.Shortcut)))
        {
            if (Shortcut.Parse(t.Shortcut) is { IsValid: true } s) parsed.Add((t, s));
            else failed.Add(new FailedShortcut(t.Name, t.Shortcut, Text.Get("ShortcutInvalid")));
        }

        // 重複しているキーは、どの対象にも登録しない
        var duplicates = ShortcutValidation.Duplicates(parsed.Select(p => p.Shortcut));
        foreach (var (target, shortcut) in parsed)
        {
            if (duplicates.Contains(shortcut))
                failed.Add(new FailedShortcut(target.Name, shortcut.ToString(), Text.Get("ShortcutDuplicate")));
            else if (_hotkeys.Register(shortcut, target.Repeat) is { } id)
                _targets[id] = target;
            else
                failed.Add(new FailedShortcut(target.Name, shortcut.ToString(), Text.Get("ShortcutRegisterFailed")));
        }

        Failed = failed;
        return failed;
    }
}
