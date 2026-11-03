namespace Dimmer.Core;

/// <summary>Monitor の特定キーと表示名を決める。</summary>
public static class MonitorIdentity
{
    /// <summary>
    /// 特定キー。EDID のシリアル番号 + 型番で決め、シリアル番号が無ければ接続パスにフォールバックする。
    /// フォールバックしたキーは、挿すポートを変えると変わる。
    /// </summary>
    public static string Key(EdidInfo? edid, string connectionPath) =>
        edid?.SerialNumber is { } serial
            ? $"{edid.ManufacturerId}{edid.ProductCode}:{serial}"
            : $"path:{connectionPath}";

    /// <summary>
    /// 表示名。ユーザーが付けた名前を優先し、未設定なら型番名を使う。
    /// 同じ型番名が複数あるときは、並び順に <c>DELL U2723QE (1)</c>、<c>(2)</c> と番号を付ける。
    /// </summary>
    public static IReadOnlyList<string> DisplayNames(
        IReadOnlyList<(string Key, string ModelName)> monitors,
        IReadOnlyDictionary<string, string> userNames)
    {
        var counts = monitors.GroupBy(m => m.ModelName).ToDictionary(g => g.Key, g => g.Count());
        var seen = new Dictionary<string, int>();
        var names = new List<string>(monitors.Count);
        foreach (var (key, model) in monitors)
        {
            var n = seen[model] = seen.GetValueOrDefault(model) + 1;
            if (userNames.TryGetValue(key, out var userName) && !string.IsNullOrWhiteSpace(userName))
                names.Add(userName);
            else
                names.Add(counts[model] > 1 ? $"{model} ({n})" : model);
        }
        return names;
    }

    /// <summary>
    /// <c>\\?\DISPLAY#DEL4099#5&amp;372e392c&amp;1&amp;UID4352#{e6f07b5f-...}</c> のようなデバイスパスを、
    /// <c>DISPLAY\DEL4099\5&amp;372e392c&amp;1&amp;UID4352</c> のようなデバイスインスタンス ID に変換する。
    /// </summary>
    public static string InstanceIdFromDevicePath(string devicePath)
    {
        var s = devicePath.StartsWith(@"\\?\") ? devicePath[4..] : devicePath;
        var guid = s.LastIndexOf("#{", StringComparison.Ordinal);
        if (guid >= 0) s = s[..guid];
        return s.Replace('#', '\\');
    }
}
