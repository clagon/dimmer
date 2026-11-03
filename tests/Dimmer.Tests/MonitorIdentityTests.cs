using Dimmer.Core;

namespace Dimmer.Tests;

public class MonitorIdentityTests
{
    [Fact]
    public void シリアル番号があれば型番とシリアル番号で特定する()
    {
        var edid = new EdidInfo("DEL", "4099", "P6MV03B1C98L", "DELL P2314H");
        Assert.Equal("DEL4099:P6MV03B1C98L", MonitorIdentity.Key(edid, @"\\?\DISPLAY#DEL4099#a"));
        Assert.Equal(MonitorIdentity.Key(edid, "port1"), MonitorIdentity.Key(edid, "port2"));
    }

    [Fact]
    public void シリアル番号が無ければ接続パスにフォールバックする()
    {
        var edid = new EdidInfo("DEL", "4099", null, "DELL P2314H");
        Assert.Equal("path:port1", MonitorIdentity.Key(edid, "port1"));
        Assert.Equal("path:port1", MonitorIdentity.Key(null, "port1"));
    }

    [Fact]
    public void 同じ型番が複数あれば番号を付け_ユーザーの名前を優先する()
    {
        var monitors = new[] { ("a", "DELL U2723QE"), ("b", "DELL P2314H"), ("c", "DELL U2723QE") };
        Assert.Equal(["DELL U2723QE (1)", "DELL P2314H", "DELL U2723QE (2)"],
            MonitorIdentity.DisplayNames(monitors, new Dictionary<string, string>()));
        Assert.Equal(["DELL U2723QE (1)", "DELL P2314H", "左"],
            MonitorIdentity.DisplayNames(monitors, new Dictionary<string, string> { ["c"] = "左" }));
    }

    [Fact]
    public void デバイスパスをインスタンスIDに変換する()
    {
        Assert.Equal(@"DISPLAY\DEL4099\5&372e392c&1&UID4352",
            MonitorIdentity.InstanceIdFromDevicePath(@"\\?\DISPLAY#DEL4099#5&372e392c&1&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}"));
    }
}
