namespace Dimmer.Core;

/// <summary>Apply で 1 台の Monitor に書き込む内容（書き込む順に並んでいる）。</summary>
public sealed record MonitorWrites(string Monitor, IReadOnlyList<(Setting Setting, int Value)> Writes);

/// <summary>Apply でスキップした指定。</summary>
public sealed record Skipped(ProfileEntry Entry, SkipReason Reason);

public enum SkipReason
{
    MonitorNotFound,
    NotSupported,
}

/// <summary>Profile を、Monitor ごとの書き込みの並びとスキップに分ける。</summary>
public static class ApplyPlan
{
    /// <summary>Monitor 内での書き込み順。入力ソースや電源状態を変えると応答が遅れるので最後に回す。</summary>
    private static readonly Setting[] Order =
        [Setting.Brightness, Setting.Contrast, Setting.Volume, Setting.ColorPreset, Setting.Orientation, Setting.InputSource, Setting.PowerMode];

    /// <param name="supported">接続中の Monitor の特定キー → 対応している Setting と選択肢（連続値は空）</param>
    public static (List<MonitorWrites> Writes, List<Skipped> Skipped) Build(
        Profile profile,
        IReadOnlyDictionary<string, IReadOnlyDictionary<Setting, IReadOnlyList<int>>> supported)
    {
        var skipped = new List<Skipped>();
        var writes = new List<MonitorWrites>();

        foreach (var group in profile.Entries.Where(e => e.Enabled).GroupBy(e => e.Monitor))
        {
            if (!supported.TryGetValue(group.Key, out var settings))
            {
                skipped.AddRange(group.Select(e => new Skipped(e, SkipReason.MonitorNotFound)));
                continue;
            }

            var ok = new List<ProfileEntry>();
            foreach (var e in group)
            {
                var valid = settings.TryGetValue(e.Setting, out var choices)
                    && (e.Setting.IsContinuous() ? e.Value is >= 0 and <= 100 : choices.Contains(e.Value));
                if (valid) ok.Add(e);
                else skipped.Add(new Skipped(e, SkipReason.NotSupported));
            }
            // 同じ Setting が重複していたら後ろの指定を使う
            var ordered = ok.GroupBy(e => e.Setting).Select(g => g.Last())
                .OrderBy(e => Array.IndexOf(Order, e.Setting))
                .Select(e => (e.Setting, e.Value)).ToList();
            if (ordered.Count > 0) writes.Add(new MonitorWrites(group.Key, ordered));
        }
        return (writes, skipped);
    }
}
