namespace Dimmer.Core;

/// <summary>DDC/CI の capabilities 文字列から、Monitor が対応している Setting と選択肢を判定する。</summary>
public static class Capabilities
{
    /// <summary>
    /// <c>(prot(monitor)type(lcd)vcp(10 12 14(05 08 0B) 60(0F 11) D6(01 04))mccs_ver(2.1))</c> の
    /// <c>vcp(...)</c> 部分を「VCP コード → 報告された値の一覧」にする。値の一覧が無いコードは空の一覧。
    /// </summary>
    public static Dictionary<byte, List<byte>> ParseVcp(string capabilities)
    {
        var result = new Dictionary<byte, List<byte>>();
        var start = IndexOfTag(capabilities, "vcp");
        if (start < 0) return result;

        byte? last = null;
        var depth = 0;
        var token = "";
        for (var i = start; i < capabilities.Length; i++)
        {
            var c = capabilities[i];
            if (Uri.IsHexDigit(c)) { token += c; continue; }

            Flush();
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                if (--depth == 0) break;
            }
        }
        return result;

        void Flush()
        {
            if (token.Length == 0) return;
            // 2 桁ずつ区切る（スペース無しで "0F11" のように並べる機種がある）
            for (var j = 0; j + 1 < token.Length; j += 2)
            {
                var b = Convert.ToByte(token.Substring(j, 2), 16);
                if (depth == 1) { last = b; result.TryAdd(b, []); }
                else if (last is { } code) result[code].Add(b);
            }
            token = "";
        }
    }

    /// <summary>
    /// DDC/CI で操作できる Setting と、その選択肢（連続値は空）。
    /// 選択式で値が 1 つも報告されていない Setting は選べないので含めない。Orientation は DDC/CI と無関係なので含めない。
    /// </summary>
    public static Dictionary<Setting, IReadOnlyList<int>> SupportedSettings(Dictionary<byte, List<byte>> vcp)
    {
        var result = new Dictionary<Setting, IReadOnlyList<int>>();
        foreach (var s in Enum.GetValues<Setting>())
        {
            if (s.VcpCode() is not { } code || !vcp.TryGetValue(code, out var values)) continue;
            if (s.IsContinuous()) result[s] = [];
            else if (values.Count > 0) result[s] = values.Distinct().Select(v => (int)v).ToList();
        }
        return result;
    }

    // "vcp(" の位置（"vcpname(" などと取り違えないよう直後が '(' のものを探す）。返すのは '(' の位置
    private static int IndexOfTag(string s, string tag)
    {
        for (var i = s.IndexOf(tag, StringComparison.OrdinalIgnoreCase); i >= 0; i = s.IndexOf(tag, i + 1, StringComparison.OrdinalIgnoreCase))
        {
            var j = i + tag.Length;
            while (j < s.Length && s[j] == ' ') j++;
            if (j < s.Length && s[j] == '(' && (i == 0 || !char.IsLetterOrDigit(s[i - 1]) && s[i - 1] != '_')) return j;
        }
        return -1;
    }
}
