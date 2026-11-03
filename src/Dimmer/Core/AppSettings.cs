using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dimmer.Core;

/// <summary>設定ファイル（settings.json）の中身。</summary>
public sealed class AppSettings
{
    public List<Profile> Profiles { get; set; } = [];

    public List<ProfileCycle> Cycles { get; set; } = [];

    public List<Adjustment> Adjustments { get; set; } = [];

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

/// <summary>特定の Setting を現在値から一定量だけ増減させる相対操作。名前は持たず、表示名は中身から作る。</summary>
public sealed class Adjustment
{
    public const string CursorMonitor = "cursor";
    public const string AllMonitors = "all";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>明るさ・コントラスト・音量のいずれか。</summary>
    public Setting Setting { get; set; } = Setting.Brightness;

    /// <summary>対象の Monitor。<see cref="CursorMonitor"/>、<see cref="AllMonitors"/>、または Monitor の特定キー。</summary>
    public string Target { get; set; } = CursorMonitor;

    /// <summary>1 回あたりの変化量（符号付きの %）。</summary>
    public int Delta { get; set; } = 10;

    /// <summary>割り当てた Shortcut。無ければ null。</summary>
    public string? Shortcut { get; set; }

    /// <summary>使えるか（連続値の Setting で、変化量が 0 でなく、対象がある）。</summary>
    [JsonIgnore]
    public bool IsValid => Setting.IsContinuous() && Delta != 0 && !string.IsNullOrWhiteSpace(Target);

    /// <summary>現在値に <paramref name="steps"/> 回分の変化量を足し、0〜100% に収める。</summary>
    public int Apply(int current, int steps) => Math.Clamp(current + Delta * steps, 0, 100);
}

/// <param name="Monitor">Monitor の特定キー</param>
/// <param name="Value">連続値は 0〜100%、選択式は VCP のコード値、Orientation は度数</param>
/// <param name="Enabled">Apply に含めるか（設定画面のチェック）。含めない指定も値を残しておける</param>
public sealed record ProfileEntry(string Monitor, Setting Setting, int Value, bool Enabled = true);
