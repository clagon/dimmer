using Dimmer.Core;

namespace Dimmer.Tests;

public sealed class SettingsFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;
    private string PathOf => Path.Combine(_dir, "settings.json");

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void ファイルが無ければ空の設定()
    {
        var file = new SettingsFile(PathOf);
        Assert.Null(file.Load());
        Assert.Empty(file.Settings.Profiles);
    }

    [Fact]
    public void 保存して読み直すと同じ内容()
    {
        var file = new SettingsFile(PathOf);
        file.Settings.Profiles.Add(new Profile { Id = "work", Name = "作業", Entries = [new("DEL4099:X", Setting.InputSource, 0x11)] });
        file.Settings.MonitorNames["DEL4099:X"] = "左";
        file.Settings.LastAppliedProfileId = "work";
        file.Save();

        var reloaded = new SettingsFile(PathOf);
        Assert.Null(reloaded.Load());
        var p = Assert.Single(reloaded.Settings.Profiles);
        Assert.Equal(("work", "作業"), (p.Id, p.Name));
        Assert.Equal(new ProfileEntry("DEL4099:X", Setting.InputSource, 0x11), p.Entries.Single());
        Assert.Equal("左", reloaded.Settings.MonitorNames["DEL4099:X"]);
        Assert.Equal("work", reloaded.Settings.LastAppliedProfileId);
    }

    [Fact]
    public void 手で書いたJSONを読める()
    {
        File.WriteAllText(PathOf, """
            {
              // コメントと末尾のカンマを許す
              "profiles": [
                { "id": "night", "name": "夜", "entries": [ { "monitor": "M", "setting": "brightness", "value": 20 }, ] }
              ]
            }
            """);
        var file = new SettingsFile(PathOf);
        Assert.Null(file.Load());
        Assert.Equal(Setting.Brightness, file.Settings.Profiles.Single().Entries.Single().Setting);
    }

    [Fact]
    public void 壊れたJSONは空の設定で読み込み_元のファイルを上書きしない()
    {
        File.WriteAllText(PathOf, "{ broken");
        var file = new SettingsFile(PathOf);
        Assert.NotNull(file.Load());
        Assert.Empty(file.Settings.Profiles);

        file.Settings.LastAppliedProfileId = "x";
        file.Save();
        Assert.Equal("{ broken", File.ReadAllText(PathOf));
    }

    [Theory]
    [InlineData("""{ "profiles": null }""")]
    [InlineData("""{ "profiles": [ null ] }""")]
    [InlineData("""{ "profiles": [ { "id": "a", "name": "a", "entries": null } ] }""")]
    [InlineData("""{ "profiles": [ { "id": "a", "name": "a", "entries": [ null ] } ] }""")]
    [InlineData("""{ "profiles": [ { "id": null, "name": "a" } ] }""")]
    [InlineData("""{ "monitorNames": null }""")]
    [InlineData("null")]
    public void 必須の項目がnullなら壊れたファイルとして扱う(string json)
    {
        File.WriteAllText(PathOf, json);
        var file = new SettingsFile(PathOf);
        Assert.NotNull(file.Load());
        Assert.Empty(file.Settings.Profiles);
        file.Save();
        Assert.Equal(json, File.ReadAllText(PathOf));
    }
}
