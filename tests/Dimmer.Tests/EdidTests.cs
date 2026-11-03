using System.Text;
using Dimmer.Core;

namespace Dimmer.Tests;

public class EdidTests
{
    private static byte[] Build(uint numericSerial, string? serialText, string? name)
    {
        var e = new byte[128];
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(e, 0);
        e[8] = 0x10; e[9] = 0xAC;          // DEL
        e[10] = 0x99; e[11] = 0x40;        // 0x4099
        BitConverter.GetBytes(numericSerial).CopyTo(e, 12);
        void Descriptor(int at, byte tag, string text)
        {
            e[at + 3] = tag;
            var b = Encoding.ASCII.GetBytes((text + "\n").PadRight(13));
            b.CopyTo(e, at + 5);
        }
        if (serialText != null) Descriptor(72, 0xFF, serialText);
        if (name != null) Descriptor(90, 0xFC, name);
        e[54] = 0x01; // 先頭は詳細タイミング（記述子ではない）
        return e;
    }

    [Fact]
    public void 製造元_製品コード_シリアル番号_型番名を取り出す()
    {
        Assert.Equal(new EdidInfo("DEL", "4099", "P6MV03B1C98L", "DELL P2314H"), Edid.Parse(Build(0, "P6MV03B1C98L", "DELL P2314H")));
    }

    [Fact]
    public void 文字列のシリアル番号が無ければ数値を使い_未設定値は無視する()
    {
        Assert.Equal("12345", Edid.Parse(Build(12345, null, "X"))!.SerialNumber);
        Assert.Null(Edid.Parse(Build(0, null, "X"))!.SerialNumber);
        Assert.Null(Edid.Parse(Build(0x01010101, null, "X"))!.SerialNumber);
    }

    [Fact]
    public void ヘッダーが違えばnull() => Assert.Null(Edid.Parse(new byte[128]));
}
