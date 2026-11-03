using Dimmer.Core;
using Dimmer.Monitors;
using Monitor = Dimmer.Monitors.Monitor;

namespace Dimmer.Profiles;

/// <summary>Profile を実際の Monitor に Apply する。</summary>
public static class ProfileApplier
{
    /// <summary>
    /// Monitor ごとに並行して書き込む。Monitor 内では <see cref="ApplyPlan"/> の順に、現在値を読み直して違うものだけを書き込む。
    /// </summary>
    /// <param name="monitorNames">見つからない Monitor の表示に使う Monitor Name（特定キー → 名前）</param>
    /// <returns>スキップや書き込みの失敗。無ければ空</returns>
    public static async Task<List<string>> ApplyAsync(Profile profile, IReadOnlyList<Monitor> monitors, IReadOnlyDictionary<string, string> monitorNames)
    {
        var byKey = monitors.GroupBy(m => m.Key).ToDictionary(g => g.Key, g => g.First());
        var (writes, skipped) = ApplyPlan.Build(profile, byKey.ToDictionary(kv => kv.Key, kv => kv.Value.Settings));

        var problems = skipped.Select(s => s.Reason == SkipReason.MonitorNotFound
            ? Text.Format("ApplyMonitorNotFound", monitorNames.GetValueOrDefault(s.Entry.Monitor) ?? s.Entry.Monitor, Text.Setting(s.Entry.Setting))
            : Text.Format("ApplyNotSupported", byKey[s.Entry.Monitor].DisplayName, Text.Setting(s.Entry.Setting), Text.Value(s.Entry.Setting, s.Entry.Value)))
            .ToList();

        var failures = await Task.WhenAll(writes.Select(w => Task.Run(() => Write(byKey[w.Monitor], w.Writes))));
        problems.AddRange(failures.SelectMany(f => f));
        return problems;
    }

    private static List<string> Write(Monitor monitor, IReadOnlyList<(Setting Setting, int Value)> writes)
    {
        var failures = new List<string>();
        foreach (var (setting, value) in writes)
        {
            try
            {
                // 他のアプリ（PowerToys など）が変えた値とのずれを吸収するため、毎回読み直す。読めなければ書いてみる
                int? current;
                try { current = MonitorSettings.Read(monitor, setting); }
                catch (SettingException) { current = null; }
                if (current != value) MonitorSettings.Write(monitor, setting, value);
            }
            catch (SettingException e)
            {
                Log.Write($"Apply failed: {e.Message}");
                failures.Add(Text.Format("ApplyFailed", monitor.DisplayName, Text.Setting(setting)));
            }
        }
        return failures;
    }
}
