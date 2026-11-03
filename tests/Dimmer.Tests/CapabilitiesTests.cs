using Dimmer.Core;

namespace Dimmer.Tests;

public class CapabilitiesTests
{
    // DELL P2314H の実際の capabilities（一部）
    private const string Dell =
        "(prot(monitor)type(LCD)model(P2314H)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B 0C) 16 18 1A 52 60(01 0F 11 ) AA(01 02) AC AE B2 B6 C6 C8 C9 D6(01 04 05) DC(00 02 03 05 ) DF FD)mccs_ver(2.1)mswhql(1))";

    [Fact]
    public void vcpのコードと値の一覧を取り出す()
    {
        var vcp = Capabilities.ParseVcp(Dell);
        Assert.Empty(vcp[0x10]);
        Assert.Equal([0x05, 0x08, 0x0B, 0x0C], vcp[0x14]);
        Assert.Equal([0x01, 0x0F, 0x11], vcp[0x60]);
        Assert.Equal([0x01, 0x04, 0x05], vcp[0xD6]);
        Assert.False(vcp.ContainsKey(0x62));
    }

    [Fact]
    public void 報告されていないSettingは非対応と判定する()
    {
        var supported = Capabilities.SupportedSettings(Capabilities.ParseVcp(Dell));
        Assert.Equal([Setting.Brightness, Setting.Contrast, Setting.InputSource, Setting.ColorPreset, Setting.PowerMode],
            supported.Keys.Order().ToArray());
        Assert.Equal([0x01, 0x0F, 0x11], supported[Setting.InputSource]);
    }

    [Fact]
    public void 値の一覧が無い選択式は非対応と判定する()
    {
        var supported = Capabilities.SupportedSettings(Capabilities.ParseVcp("(vcp(10 60 62))"));
        Assert.Equal([Setting.Brightness, Setting.Volume], supported.Keys.Order().ToArray());
    }

    [Fact]
    public void 区切りのスペースが無い値も2桁ずつ読む()
    {
        Assert.Equal([0x0F, 0x11], Capabilities.ParseVcp("(vcp(1012 60(0F11)))")[0x60]);
        Assert.True(Capabilities.ParseVcp("(vcp(1012 60(0F11)))").ContainsKey(0x12));
    }

    [Fact]
    public void vcpが無ければ空() => Assert.Empty(Capabilities.ParseVcp("(prot(monitor)vcpname(10(Brightness)))"));
}
