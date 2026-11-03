namespace Dimmer.Core;

[Flags]
public enum Modifiers
{
    None = 0,
    // RegisterHotKey の MOD_* と同じ値
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>グローバルなキーの組み合わせ。設定ファイルには <c>Ctrl+Alt+B</c> のような文字列で書く。</summary>
/// <param name="Key">仮想キーコード（VK_*）</param>
public sealed record Shortcut(Modifiers Modifiers, int Key)
{
    /// <summary>
    /// 割り当てられるか。Ctrl・Alt・Shift・Win のいずれかとの組み合わせが必須。
    /// ただし F13〜F24 は物理キーボードにほぼ無く誤爆しないので、修飾キーなしでも割り当てられる。
    /// </summary>
    public bool IsValid => Keys.Name(Key) is not null && (Modifiers != Modifiers.None || Key is >= Keys.F13 and <= Keys.F24);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(Modifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(Modifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(Modifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(Modifiers.Win)) parts.Add("Win");
        parts.Add(Keys.Name(Key) ?? $"0x{Key:X2}");
        return string.Join("+", parts);
    }

    /// <summary><c>Ctrl+Alt+B</c> を読む。大文字小文字と空白は区別しない。読めなければ null（検証は <see cref="IsValid"/>）。</summary>
    public static Shortcut? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var modifiers = Modifiers.None;
        int? key = null;
        foreach (var raw in text.Split('+'))
        {
            var part = raw.Trim();
            if (Enum.TryParse<Modifiers>(part, true, out var m) && m != Modifiers.None && Enum.IsDefined(m)) { modifiers |= m; continue; }
            if (key is not null || Keys.Code(part) is not { } code) return null;
            key = code;
        }
        return key is { } k ? new Shortcut(modifiers, k) : null;
    }
}

/// <summary>Shortcut に使えるキーの名前と仮想キーコード。</summary>
public static class Keys
{
    public const int F1 = 0x70, F12 = 0x7B, F13 = 0x7C, F24 = 0x87;

    private static readonly Dictionary<string, int> ByName = Build();
    private static readonly Dictionary<int, string> ByCode = ByName.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    /// <summary>名前の一覧（ドロップダウン用。表示順）。</summary>
    public static IEnumerable<string> Names => ByCode.Values;

    public static int? Code(string name) => ByName.TryGetValue(name, out var c) ? c : null;

    public static string? Name(int code) => ByCode.GetValueOrDefault(code);

    private static Dictionary<string, int> Build()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 'A'; c <= 'Z'; c++) d[c.ToString()] = c;
        for (var c = '0'; c <= '9'; c++) d[c.ToString()] = c;
        for (var i = 1; i <= 24; i++) d[$"F{i}"] = F1 + i - 1;
        for (var i = 0; i <= 9; i++) d[$"Num{i}"] = 0x60 + i;
        (string, int)[] named =
        [
            ("Space", 0x20), ("PageUp", 0x21), ("PageDown", 0x22), ("End", 0x23), ("Home", 0x24),
            ("Left", 0x25), ("Up", 0x26), ("Right", 0x27), ("Down", 0x28),
            ("Insert", 0x2D), ("Delete", 0x2E), ("Pause", 0x13), ("ScrollLock", 0x91), ("PrintScreen", 0x2C),
            ("NumMultiply", 0x6A), ("NumAdd", 0x6B), ("NumSubtract", 0x6D), ("NumDecimal", 0x6E), ("NumDivide", 0x6F),
            ("VolumeMute", 0xAD), ("VolumeDown", 0xAE), ("VolumeUp", 0xAF),
            ("MediaNext", 0xB0), ("MediaPrevious", 0xB1), ("MediaStop", 0xB2), ("MediaPlayPause", 0xB3),
            ("Minus", 0xBD), ("Plus", 0xBB), ("Comma", 0xBC), ("Period", 0xBE),
        ];
        foreach (var (n, c) in named) d[n] = c;
        return d;
    }
}

/// <summary>Shortcut の割り当ての検証。</summary>
public static class ShortcutValidation
{
    /// <summary>アプリ内で同じキーが複数の対象に割り当てられているものを返す。</summary>
    public static HashSet<Shortcut> Duplicates(IEnumerable<Shortcut> shortcuts) =>
        shortcuts.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
}
