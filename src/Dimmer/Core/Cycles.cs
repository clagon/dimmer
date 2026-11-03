namespace Dimmer.Core;

/// <summary>Profile Cycle が使えない理由。</summary>
public enum CycleProblem
{
    None,
    NoName,
    TooFewProfiles,
    DuplicateProfiles,
}

public static class Cycles
{
    /// <summary>
    /// 次に Apply する Profile の位置。直近に Apply された Profile の次で、末尾の次は先頭。
    /// 直近の Profile が並びに無ければ先頭。
    /// </summary>
    public static int NextIndex(IReadOnlyList<string> profileIds, string? lastAppliedProfileId)
    {
        var i = lastAppliedProfileId is null ? -1 : IndexOf(profileIds, lastAppliedProfileId);
        return i < 0 ? 0 : (i + 1) % profileIds.Count;
    }

    /// <summary>使えるか。名前が必須で、Profile は 2 つ以上・重複なし。</summary>
    public static CycleProblem Problem(ProfileCycle cycle) =>
        string.IsNullOrWhiteSpace(cycle.Name) ? CycleProblem.NoName
        : cycle.ProfileIds.Count < 2 ? CycleProblem.TooFewProfiles
        : cycle.ProfileIds.Distinct().Count() != cycle.ProfileIds.Count ? CycleProblem.DuplicateProfiles
        : CycleProblem.None;

    /// <summary>
    /// 存在しない Profile を Cycle から取り除く（Profile の削除や、手で書いた設定ファイルの誤り）。
    /// その結果 2 つ未満になった Cycle は <see cref="Problem"/> で使えないと判定される。
    /// </summary>
    public static void RemoveMissingProfiles(AppSettings settings)
    {
        var ids = settings.Profiles.Select(p => p.Id).ToHashSet();
        foreach (var c in settings.Cycles) c.ProfileIds.RemoveAll(id => !ids.Contains(id));
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++) if (list[i] == value) return i;
        return -1;
    }
}
