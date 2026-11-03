using Dimmer.Core;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Dimmer;

/// <summary>Strings/&lt;言語&gt;/Resources.resw の UI 文字列。</summary>
public static class Text
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string name) => Loader.GetString(name);

    public static string Format(string name, params object[] args) => string.Format(Get(name), args);

    /// <summary>Setting の表示名（例: 明るさ）。</summary>
    public static string Setting(Setting setting) => Get($"Setting_{setting}");

    /// <summary>値の表示（連続値は %、選択式は MCCS の標準名、Orientation は度数）。</summary>
    public static string Value(Setting setting, int value) => setting.IsContinuous() ? $"{value}%" : Vcp.ValueName(setting, value);
}
