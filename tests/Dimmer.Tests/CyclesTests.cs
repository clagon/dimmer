using Dimmer.Core;

namespace Dimmer.Tests;

public class CyclesTests
{
    private static readonly string[] Abc = ["a", "b", "c"];

    [Fact]
    public void 押すたびにa_b_c_aと進む()
    {
        string? last = null;
        var applied = new List<string>();
        for (var i = 0; i < 4; i++) { last = Abc[Cycles.NextIndex(Abc, last)]; applied.Add(last); }
        Assert.Equal(["a", "b", "c", "a"], applied);
    }

    [Fact]
    public void 他の経路でApplyされたProfileの次に進む() => Assert.Equal(2, Cycles.NextIndex(Abc, "b"));

    [Fact]
    public void 直近のProfileが並びに無ければ先頭()
    {
        Assert.Equal(0, Cycles.NextIndex(Abc, "x"));
        Assert.Equal(0, Cycles.NextIndex(Abc, null));
    }

    [Theory]
    [InlineData("名前", new[] { "a", "b" }, CycleProblem.None)]
    [InlineData(" ", new[] { "a", "b" }, CycleProblem.NoName)]
    [InlineData("名前", new[] { "a" }, CycleProblem.TooFewProfiles)]
    [InlineData("名前", new string[0], CycleProblem.TooFewProfiles)]
    [InlineData("名前", new[] { "a", "b", "a" }, CycleProblem.DuplicateProfiles)]
    public void 名前と個数と重複を検証する(string name, string[] ids, CycleProblem expected) =>
        Assert.Equal(expected, Cycles.Problem(new ProfileCycle { Name = name, ProfileIds = [.. ids] }));

    [Fact]
    public void 削除されたProfileを取り除き_2つ未満になったCycleは使えない()
    {
        var settings = new AppSettings
        {
            Profiles = [new() { Id = "a" }, new() { Id = "c" }],
            Cycles =
            [
                new() { Name = "x", ProfileIds = ["a", "b", "c"] },
                new() { Name = "y", ProfileIds = ["a", "b"] },
            ],
        };
        Cycles.RemoveMissingProfiles(settings);
        Assert.Equal(["a", "c"], settings.Cycles[0].ProfileIds);
        Assert.Equal(CycleProblem.None, Cycles.Problem(settings.Cycles[0]));
        Assert.Equal(["a"], settings.Cycles[1].ProfileIds);
        Assert.Equal(CycleProblem.TooFewProfiles, Cycles.Problem(settings.Cycles[1]));
    }
}
