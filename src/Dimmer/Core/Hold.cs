namespace Dimmer.Core;

/// <summary>
/// Adjustment の Shortcut を押しっぱなしにしたときの増減の回数。キーボードのキーリピートは使わない。
/// 押した瞬間に 1 回、<see cref="InitialDelay"/> 押し続けたら以後 <see cref="RepeatInterval"/> ごとに 1 回。
/// </summary>
public static class Hold
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(0.4);
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(0.1);

    /// <summary>押し始めから <paramref name="held"/> 経った時点までの合計の回数。</summary>
    public static int StepsAt(TimeSpan held) =>
        held < InitialDelay ? 1 : 2 + (int)((held - InitialDelay).Ticks / RepeatInterval.Ticks);
}

/// <summary>
/// 最新の値だけを 1 本ずつ書き込む。書き込み中に届いた値は、最後のものだけを残して途中の値を捨てる。
/// キーを離したあとに古い値が遅れて書き込まれることはない（最後に書き込まれるのは常に最後に渡した値）。
/// </summary>
public sealed class LatestWriter(Action<int> write)
{
    private readonly Lock _gate = new();
    private int? _pending;
    private bool _running;
    private Task _worker = Task.CompletedTask;

    /// <summary>書き込みが終わるまで待つための Task（書き込むものが無ければ完了済み）。</summary>
    public Task Idle { get { lock (_gate) return _worker; } }

    public void Post(int value)
    {
        lock (_gate)
        {
            _pending = value;
            if (_running) return;
            _running = true;
            _worker = Task.Run(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            int value;
            lock (_gate)
            {
                if (_pending is not { } v) { _running = false; return; }
                value = v;
                _pending = null;
            }
            write(value);
        }
    }
}
