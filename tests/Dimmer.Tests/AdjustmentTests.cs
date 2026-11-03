using Dimmer.Core;

namespace Dimmer.Tests;

public class AdjustmentTests
{
    [Theory]
    [InlineData(50, 10, 1, 60)]
    [InlineData(95, 10, 1, 100)]   // 100% で止まる
    [InlineData(95, 10, 5, 100)]
    [InlineData(5, -10, 1, 0)]     // 0% で止まる
    [InlineData(50, -10, 3, 20)]
    public void 結果を0から100に収める(int current, int delta, int steps, int expected) =>
        Assert.Equal(expected, new Adjustment { Delta = delta }.Apply(current, steps));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(399, 1)]   // 0.4 秒までは押した瞬間の 1 回だけ
    [InlineData(400, 2)]
    [InlineData(499, 2)]
    [InlineData(500, 3)]   // 以後 0.1 秒ごとに 1 回
    [InlineData(1400, 12)]
    public void 押しっぱなしの回数(int ms, int steps) => Assert.Equal(steps, Hold.StepsAt(TimeSpan.FromMilliseconds(ms)));

    [Theory]
    [InlineData(Setting.Brightness, 10, true)]
    [InlineData(Setting.Volume, -5, true)]
    [InlineData(Setting.InputSource, 10, false)]
    [InlineData(Setting.Brightness, 0, false)]
    public void 連続値で変化量が0でなければ使える(Setting setting, int delta, bool valid) =>
        Assert.Equal(valid, new Adjustment { Setting = setting, Delta = delta }.IsValid);

    [Fact]
    public async Task 書き込み中に届いた途中の値は捨て_最後の値を書き込む()
    {
        var written = new List<int>();
        var first = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var writer = new LatestWriter(v =>
        {
            lock (written) written.Add(v);
            if (v == 1) { first.Set(); release.Wait(); }
        });

        writer.Post(1);
        first.Wait();          // 1 を書き込んでいる最中に
        writer.Post(2);
        writer.Post(3);
        writer.Post(4);        // 2 と 3 は捨てられる
        release.Set();
        await writer.Idle;

        Assert.Equal([1, 4], written);
    }

    [Fact]
    public async Task 書き込みが終わった後の値も書き込む()
    {
        var written = new List<int>();
        var writer = new LatestWriter(v => { lock (written) written.Add(v); });
        writer.Post(1);
        await writer.Idle;
        writer.Post(2);
        await writer.Idle;
        Assert.Equal([1, 2], written);
    }
}
