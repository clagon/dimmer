namespace Dimmer.Core;

/// <summary>Monitor が持つ操作可能な 1 項目。</summary>
public enum Setting
{
    Brightness,
    Contrast,
    Volume,
    InputSource,
    ColorPreset,
    PowerMode,
    Orientation,
}

public static class SettingExtensions
{
    /// <summary>0〜100% で扱う連続値か（そうでなければ選択肢から選ぶ）。</summary>
    public static bool IsContinuous(this Setting s) => s is Setting.Brightness or Setting.Contrast or Setting.Volume;

    /// <summary>DDC/CI（MCCS）の VCP コード。Orientation は DDC/CI では扱わないので null。</summary>
    public static byte? VcpCode(this Setting s) => s switch
    {
        Setting.Brightness => 0x10,
        Setting.Contrast => 0x12,
        Setting.ColorPreset => 0x14,
        Setting.InputSource => 0x60,
        Setting.Volume => 0x62,
        Setting.PowerMode => 0xD6,
        _ => null,
    };

    /// <summary>Orientation の選択肢（度）。</summary>
    public static readonly IReadOnlyList<int> Orientations = [0, 90, 180, 270];
}
