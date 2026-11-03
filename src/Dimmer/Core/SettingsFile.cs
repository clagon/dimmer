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
            Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), AppSettings.Json) ?? new AppSettings();
            return null;
        }
        catch (JsonException e)
        {
            _broken = true;
            Settings = new AppSettings();
            return e.Message;
        }
    }

    /// <summary>保存する。読み込んだファイルが壊れていた場合は、元のファイルを残すため何もしない。</summary>
    public void Save()
    {
        if (_broken) return;
        // 書きかけで落ちても壊れないよう、一時ファイルに書いてから置き換える
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Settings, AppSettings.Json));
        File.Move(temp, path, overwrite: true);
    }
}
