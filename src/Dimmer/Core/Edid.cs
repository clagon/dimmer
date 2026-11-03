using System.Text;

namespace Dimmer.Core;

/// <summary>EDID から Monitor の特定に使う項目だけを取り出したもの。</summary>
/// <param name="ManufacturerId">3 文字の製造元 ID（例: DEL）</param>
/// <param name="ProductCode">製品コード（例: 4099）。16 進数の文字列</param>
/// <param name="SerialNumber">シリアル番号。取れなければ null</param>
/// <param name="ModelName">型番名（例: DELL P2314H）。取れなければ null</param>
public sealed record EdidInfo(string ManufacturerId, string ProductCode, string? SerialNumber, string? ModelName);

public static class Edid
{
    /// <summary>EDID の基本ブロック（128 バイト）を解析する。形式が違えば null。</summary>
    public static EdidInfo? Parse(byte[] edid)
    {
        if (edid.Length < 128 || edid[0] != 0x00 || edid[1] != 0xFF || edid[7] != 0x00) return null;

        var m = (edid[8] << 8) | edid[9];
        var manufacturer = new string([(char)('A' - 1 + ((m >> 10) & 0x1F)), (char)('A' - 1 + ((m >> 5) & 0x1F)), (char)('A' - 1 + (m & 0x1F))]);
        var product = (edid[10] | (edid[11] << 8)).ToString("X4");

        string? serial = null, name = null;
        // 18 バイトの記述子 4 つ。0xFF = シリアル番号文字列、0xFC = 型番名
        for (var i = 54; i <= 108; i += 18)
        {
            if (edid[i] != 0 || edid[i + 1] != 0) continue;
            var text = Encoding.ASCII.GetString(edid, i + 5, 13).Split('\n')[0].Trim();
            if (edid[i + 3] == 0xFF) serial = text;
            else if (edid[i + 3] == 0xFC) name = text;
        }

        // 文字列が無ければ数値のシリアル番号を使う。0 や 0x01010101 は未設定扱い
        if (string.IsNullOrEmpty(serial))
        {
            var n = BitConverter.ToUInt32(edid, 12);
            serial = n is 0 or 0x01010101 ? null : n.ToString();
        }

        return new EdidInfo(manufacturer, product, NullIfEmpty(serial), NullIfEmpty(name));
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
