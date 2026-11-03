using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dimmer.Core;

/// <summary>設定ファイル（settings.json）の中身。</summary>
public sealed class AppSettings
{
    public List<Profile> Profiles { get; set; } = [];

    /// <summary>ユーザーが付けた Monitor Name（特定キー → 名前）。</summary>
    public Dictionary<string, string> MonitorNames { get; set; } = [];

    /// <summary>直近に Apply された Profile の ID。</summary>
    public string? LastAppliedProfileId { get; set; }

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
}

/// <param name="Monitor">Monitor の特定キー</param>
/// <param name="Value">連続値は 0〜100%、選択式は VCP のコード値、Orientation は度数</param>
public sealed record ProfileEntry(string Monitor, Setting Setting, int Value);
