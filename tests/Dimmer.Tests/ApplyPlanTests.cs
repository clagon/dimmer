using Dimmer.Core;

namespace Dimmer.Tests;

public class ApplyPlanTests
{
    private static readonly Dictionary<string, IReadOnlyDictionary<Setting, IReadOnlyList<int>>> Monitors = new()
    {
        ["A"] = new Dictionary<Setting, IReadOnlyList<int>>
        {
            [Setting.Brightness] = [], [Setting.Contrast] = [], [Setting.InputSource] = [0x0F, 0x11],
            [Setting.ColorPreset] = [0x05], [Setting.PowerMode] = [0x01, 0x04], [Setting.Orientation] = [0, 90, 180, 270],
        },
        ["B"] = new Dictionary<Setting, IReadOnlyList<int>> { [Setting.Brightness] = [], [Setting.Orientation] = [0, 90, 180, 270] },
    };

    private static Profile P(params ProfileEntry[] e) => new() { Name = "p", Entries = [.. e] };

    [Fact]
    public void Monitor内は決まった順に書き込む()
    {
        var (writes, skipped) = ApplyPlan.Build(P(
            new("A", Setting.PowerMode, 1),
            new("A", Setting.InputSource, 0x11),
            new("A", Setting.Orientation, 90),
            new("A", Setting.ColorPreset, 5),
            new("A", Setting.Contrast, 50),
            new("A", Setting.Brightness, 40)), Monitors);

        Assert.Empty(skipped);
        Assert.Equal(
            [Setting.Brightness, Setting.Contrast, Setting.ColorPreset, Setting.Orientation, Setting.InputSource, Setting.PowerMode],
            writes.Single().Writes.Select(w => w.Setting));
    }

    [Fact]
    public void 見つからないMonitorと非対応のSettingと値はスキップし_残りは書き込む()
    {
        var (writes, skipped) = ApplyPlan.Build(P(
            new("A", Setting.Brightness, 40),
            new("A", Setting.Volume, 10),        // 非対応の Setting
            new("A", Setting.InputSource, 0x12), // 報告されていない値
            new("B", Setting.Brightness, 70),
            new("C", Setting.Brightness, 70)),   // 見つからない Monitor
            Monitors);

        Assert.Equal(["A", "B"], writes.Select(w => w.Monitor));
        Assert.Equal([(Setting.Brightness, 40)], writes[0].Writes);
        Assert.Equal(
            [(Setting.Volume, SkipReason.NotSupported), (Setting.InputSource, SkipReason.NotSupported), (Setting.Brightness, SkipReason.MonitorNotFound)],
            skipped.Select(s => (s.Entry.Setting, s.Reason)));
    }

    [Fact]
    public void 範囲外の連続値はスキップする()
    {
        var (writes, skipped) = ApplyPlan.Build(P(new ProfileEntry("B", Setting.Brightness, 120)), Monitors);
        Assert.Empty(writes);
        Assert.Single(skipped);
    }
}
