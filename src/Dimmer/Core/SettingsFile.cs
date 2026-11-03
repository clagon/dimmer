using System.Text.Json;

namespace Dimmer.Core;

/// <summary>
/// 設定ファイルの読み書き。読み込みは起動時の 1 回だけ（ファイルの監視はしない）。
/// 壊れたファイルを読んだときは空の設定で続け、元のファイルを上書きしないよう以後の保存をしない。
/// </summary>
public sealed class SettingsFile(string path)
{
    private bool _broken;

    public AppSettings Settings { get; private set; } = new();

    /// <summary>読み込む。ファイルが無ければ空の設定。壊れていればエラーの内容を返す。</summary>
    public string? Load()
    {
        if (!File.Exists(path)) return null;
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), AppSettings.Json);
            Settings = IsComplete(settings) ? settings! : throw new JsonException("必須の項目が null です");
            return null;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // 壊れている・他のプロセスが開いたままで読めない、のどちらでも空の設定で起動し、元のファイルは残す
            _broken = true;
            Settings = new AppSettings();
            return e.Message;
        }
    }

    // 手で書いた JSON の "profiles": null や null の要素は、読み込めても使うときに落ちるので壊れたファイルとして扱う
    private static bool IsComplete(AppSettings? s) =>
        s is { Profiles: not null, MonitorNames: not null, Cycles: not null, Adjustments: not null }
        && s.Adjustments.All(a => a is { Id: not null, Target: not null })
        && s.Cycles.All(c => c is { Id: not null, Name: not null, ProfileIds: not null } && c.ProfileIds.All(id => id is not null))
        && s.Profiles.All(p => p is { Id: not null, Name: not null, Entries: not null } && p.Entries.All(e => e is { Monitor: not null }))
        && s.MonitorNames.Values.All(v => v is not null);

    /// <summary>保存する。読み込んだファイルが壊れていた場合は、元のファイルを残すため何もしない。</summary>
    /// <returns>書き込めたか（他のプロセスが開いたままなどで書き込めなければ false。次の保存で書き込む）</returns>
    public bool Save()
    {
        if (_broken) return false;
        try
        {
            // 書きかけで落ちても壊れないよう、一時ファイルに書いてから置き換える
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Settings, AppSettings.Json));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
