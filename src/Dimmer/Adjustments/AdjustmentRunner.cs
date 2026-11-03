using System.Diagnostics;
using Dimmer.Core;
using Dimmer.Monitors;
using Dimmer.Overlay;
using Microsoft.UI.Dispatching;
using Monitor = Dimmer.Monitors.Monitor;

namespace Dimmer.Adjustments;

/// <summary>
/// Adjustment の Shortcut を押している間、押した瞬間に 1 回、0.4 秒押し続けたら 0.1 秒ごとに 1 回ずつ増減する。
/// 現在値は押し始めに 1 回だけ読み、押している間は目標値だけを積み上げ、Monitor ごとに最新の値を 1 本ずつ書き込む。
/// UI スレッドで使うこと。
/// </summary>
/// <param name="showOverlay">Overlay を出す（タイトル、詳細、%、HMONITOR）</param>
/// <param name="notifyFailure">書き込みに失敗した Monitor Name を知らせる</param>
/// <param name="gate">Profile の Apply と共有する順番待ち。古い現在値からの書き込みで、後の操作を上書きしないようにする</param>
public sealed class AdjustmentRunner(
    SemaphoreSlim gate,
    MonitorService monitors,
    Func<IReadOnlyDictionary<string, string>> monitorNames,
    Action<string, string?, int?, nint> showOverlay,
    Action<Adjustment, IReadOnlyList<string>> notifyFailure)
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(20);

    private readonly SemaphoreSlim _gate = gate;
    private readonly MonitorService _monitors = monitors;
    private readonly Func<IReadOnlyDictionary<string, string>> _monitorNames = monitorNames;
    private readonly Action<string, string?, int?, nint> _showOverlay = showOverlay;
    private readonly Action<Adjustment, IReadOnlyList<string>> _notifyFailure = notifyFailure;
    private Session? _session;

    public void Press(Adjustment adjustment)
    {
        _session?.Release();
        // 実行中に設定画面で中身を変えられても影響を受けないよう、押した時点の中身を写して使う
        var snapshot = new Adjustment { Id = adjustment.Id, Setting = adjustment.Setting, Target = adjustment.Target, Delta = adjustment.Delta, Shortcut = adjustment.Shortcut };
        _session = new Session(this, adjustment, snapshot);
        _ = _session.StartAsync();
    }

    public void Release(Adjustment adjustment)
    {
        if (_session?.Original == adjustment) _session.Release();
    }

    private sealed class Target(Monitor monitor, int current, LatestWriter writer)
    {
        public Monitor Monitor { get; } = monitor;
        public int Current { get; } = current;
        public LatestWriter Writer { get; } = writer;
        public int Value { get; set; } = current;
    }

    /// <param name="original">押された Adjustment（離されたときの照合に使う）</param>
    /// <param name="adjustment">押した時点の中身の写し（実行にはこちらを使う）</param>
    private sealed class Session(AdjustmentRunner runner, Adjustment original, Adjustment adjustment)
    {
        private readonly Stopwatch _held = Stopwatch.StartNew();
        // Cursor Monitor は押した瞬間のカーソルの位置で決める（列挙や前の操作を待つ間にカーソルが動いても変えない）
        private readonly string _cursorGdi = CursorMonitor.GdiName();
        private readonly nint _cursorHandle = CursorMonitor.Handle();
        private readonly List<string> _failed = [];
        private DispatcherQueueTimer? _timer;
        private TimeSpan? _releasedAt;
        private List<Target>? _targets;
        private Target? _shown;
        private nint _overlayMonitor;
        private int _steps;

        public Adjustment Original => original;

        public async Task StartAsync()
        {
            var lease = runner._monitors.Lease();
            var gated = false;
            try
            {
                await runner._monitors.Ready;
                // 現在値を読んでから書き込みが終わるまでを 1 つの操作として、Profile の Apply や前の Adjustment と
                // 押された順に並ぶ（続けて押したときも、前の操作の書き込みの後の現在値から増減する）
                await runner._gate.WaitAsync();
                gated = true;
                var (selected, shown) = Resolve();
                if (selected is null) return;

                // 現在値は押し始めに 1 回だけ読む。読めない Monitor は対象から外す
                var currents = await Task.Run(() => selected.Select(m =>
                {
                    try { return (m, (int?)MonitorSettings.Read(m, adjustment.Setting)); }
                    catch (SettingException e) { Log.Write($"Adjustment read failed: {e.Message}"); return (m, null); }
                }).ToList());

                _failed.AddRange(currents.Where(c => c.Item2 is null).Select(c => c.m.DisplayName));
                _targets = currents.Where(c => c.Item2 is not null)
                    .Select(c => new Target(c.m, c.Item2!.Value, new LatestWriter(v => Write(c.m, v)))).ToList();
                _shown = _targets.FirstOrDefault(t => t.Monitor == shown) ?? _targets.FirstOrDefault();

                _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
                _timer.Interval = TickInterval;
                _timer.Tick += (_, _) => Tick();
                Tick();
                if (_releasedAt is null) _timer.Start();

                // 離された後、書き込みが終わるまで物理モニターハンドルを借りておく
                while (_releasedAt is null) await Task.Delay(TickInterval);
                await Task.WhenAll(_targets.Select(t => t.Writer.Idle));
            }
            finally
            {
                if (gated) runner._gate.Release();
                lease.Dispose();
                if (_failed.Count > 0) runner._notifyFailure(adjustment, _failed.Distinct().ToList());
            }
        }

        public void Release()
        {
            _releasedAt ??= _held.Elapsed;
            if (_targets is not null) Tick();
        }

        private void Tick()
        {
            var steps = Hold.StepsAt(_releasedAt ?? _held.Elapsed);
            if (steps != _steps && _targets is not null)
            {
                _steps = steps;
                foreach (var t in _targets)
                {
                    var value = adjustment.Apply(t.Current, steps);
                    if (value == t.Value && steps > 1) continue;
                    t.Value = value;
                    t.Writer.Post(value);
                }
                if (_shown is { } s)
                    runner._showOverlay(Text.Format("OverlayAdjustmentTitle", s.Monitor.DisplayName, Text.Setting(adjustment.Setting)), $"{s.Value}%", s.Value, _overlayMonitor);
            }
            if (_releasedAt is not null) _timer?.Stop();
        }

        private void Write(Monitor monitor, int value)
        {
            try { MonitorSettings.Write(monitor, adjustment.Setting, value); }
            catch (SettingException e)
            {
                Log.Write($"Adjustment write failed: {e.Message}");
                lock (_failed) _failed.Add(monitor.DisplayName);
            }
        }

        /// <summary>対象の Monitor と、Overlay に値を出す Monitor を決める。対象が無ければ Overlay で知らせて null。</summary>
        private (List<Monitor>? Selected, Monitor? Shown) Resolve()
        {
            var all = runner._monitors.Monitors;
            var setting = adjustment.Setting;
            var cursorGdi = _cursorGdi;
            var cursor = all.FirstOrDefault(m => m.GdiDeviceName == cursorGdi);

            if (adjustment.Target == Adjustment.AllMonitors)
            {
                // 全 Monitor が対象のときは、対応していない Monitor を黙ってスキップする。Overlay は Cursor Monitor に 1 つだけ出す
                _overlayMonitor = _cursorHandle;
                var supported = all.Where(m => m.Settings.ContainsKey(setting)).ToList();
                if (supported.Count == 0) { Show(Text.Format("OverlayNoMonitorSupports", Text.Setting(setting))); return (null, null); }
                return (supported, cursor);
            }

            var target = adjustment.Target == Adjustment.CursorMonitor ? cursor : all.FirstOrDefault(m => m.Key == adjustment.Target);
            if (target is null)
            {
                _overlayMonitor = _cursorHandle;
                var name = runner._monitorNames().GetValueOrDefault(adjustment.Target) ?? adjustment.Target;
                Show(Text.Format("OverlayMonitorNotFound", adjustment.Target == Adjustment.CursorMonitor ? Text.Get("CursorMonitor") : name));
                return (null, null);
            }

            _overlayMonitor = CursorMonitor.HandleOf(target.GdiDeviceName);
            if (!target.Settings.ContainsKey(setting))
            {
                Show(Text.Format("OverlayNotSupported", target.DisplayName, Text.Setting(setting)));
                return (null, null);
            }
            return (new List<Monitor> { target }, target);
        }

        private void Show(string message) => runner._showOverlay(message, null, null, _overlayMonitor);
    }
}
