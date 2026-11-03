using Dimmer.Core;

namespace Dimmer.Tests;

public class VcpTests
{
    [Theory]
    [InlineData(50, 100u, 50u)]
    [InlineData(50, 255u, 128u)]  // 127.5 → 128
    [InlineData(33, 10u, 3u)]
    [InlineData(35, 10u, 4u)]     // 3.5 → 4
    [InlineData(100, 255u, 255u)]
    [InlineData(150, 100u, 100u)] // 範囲外は丸める
    public void パーセントを生の値に換算する(int percent, uint max, uint raw) => Assert.Equal(raw, Vcp.ToRaw(percent, max));

    [Theory]
    [InlineData(128u, 255u, 50)]
    [InlineData(3u, 10u, 30)]
    [InlineData(255u, 255u, 100)]
    [InlineData(5u, 0u, 0)]
    public void 生の値をパーセントに換算する(uint raw, uint max, int percent) => Assert.Equal(percent, Vcp.ToPercent(raw, max));

    [Fact]
    public void 最大値が100以外でも往復で値が変わらない()
    {
        for (var p = 0; p <= 100; p++) Assert.Equal(p, Vcp.ToPercent(Vcp.ToRaw(p, 255), 255));
    }

    [Fact]
    public void MCCSの標準名を引き_無ければ16進数()
    {
        Assert.Equal("HDMI 1", Vcp.ValueName(Setting.InputSource, 0x11));
        Assert.Equal("DisplayPort 1", Vcp.ValueName(Setting.InputSource, 0x0F));
        Assert.Equal("6500K", Vcp.ValueName(Setting.ColorPreset, 0x05));
        Assert.Equal("0x1A", Vcp.ValueName(Setting.InputSource, 0x1A));
        Assert.Equal("90°", Vcp.ValueName(Setting.Orientation, 90));
    }
}
