using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dimmer.Core;

/// <summary>設定ファイル（settings.json）の中身。</summary>
public sealed class AppSettings
{
    public List<Profile> Profiles { get; set; } = [];

    public List<ProfileCycle> Cycles { get; set; } = [];

    /// <summary>ユーザーが付けた Monitor Name（特定キー → 名前）。</summary>
    public Dictionary<string, string> MonitorNames { get; set; } = [];

    /// <summary>直近に Apply された Profile の ID。</summary>
    public string? LastAppliedProfileId { get; set; }

    /// <summary>Shortcut の実行時に Overlay を出すか。無効でもエラーのトースト通知は出す。</summary>
    public bool OverlayEnabled { get; set; } = true;

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        // 手で書いた JSON で "setting" や "value" が抜けていたら、既定値（明るさ 0%）で Apply しないよう読み込みエラーにする
        RespectRequiredConstructorParameters = true,
        AllowTrailingCommas = true,
    };
}

/// <summary>「どの Monitor のどの Setting をどの値にするか」の部分的な指定の集まり。</summary>
public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<ProfileEntry> Entries { get; set; } = [];

    /// <summary>割り当てた Shortcut（例: Ctrl+Alt+B）。無ければ null。</summary>
    public string? Shortcut { get; set; }
}

/// <summary>名前付きの、2 つ以上の Profile を重複なく並べたもの。Shortcut を押すたびに次の Profile を Apply する。</summary>
public sealed class ProfileCycle
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    /// <summary>並べた Profile の ID。</summary>
    public List<string> ProfileIds { get; set; } = [];

    /// <summary>割り当てた Shortcut（例: Ctrl+Alt+C）。無ければ null。</summary>
    public string? Shortcut { get; set; }
}

/// <param name="Monitor">Monitor の特定キー</param>
/// <param name="Value">連続値は 0〜100%、選択式は VCP のコード値、Orientation は度数</param>
public sealed record ProfileEntry(string Monitor, Setting Setting, int Value);
