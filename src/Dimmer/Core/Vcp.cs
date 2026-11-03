namespace Dimmer.Core;

/// <summary>VCP の生の値と % の換算、選択肢の表示名（MCCS の標準名）。</summary>
public static class Vcp
{
    /// <summary>% を Monitor の生の値にする（四捨五入）。</summary>
    public static uint ToRaw(int percent, uint max) =>
        (uint)Math.Round(Math.Clamp(percent, 0, 100) * (double)max / 100, MidpointRounding.AwayFromZero);

    /// <summary>Monitor の生の値を % にする（四捨五入）。</summary>
    public static int ToPercent(uint raw, uint max) =>
        max == 0 ? 0 : (int)Math.Round(Math.Min(raw, max) * 100.0 / max, MidpointRounding.AwayFromZero);

    /// <summary>選択肢の表示名。標準名が無いコードは 16 進数（例: <c>0x1A</c>）。Orientation は度数。</summary>
    public static string ValueName(Setting setting, int code)
    {
        if (setting == Setting.Orientation) return $"{code}°";
        var names = setting switch
        {
            Setting.InputSource => InputSources,
            Setting.ColorPreset => ColorPresets,
            Setting.PowerMode => PowerModes,
            _ => null,
        };
        return names != null && names.TryGetValue(code, out var name) ? name : $"0x{code:X2}";
    }

    private static readonly Dictionary<int, string> InputSources = new()
    {
        [0x01] = "VGA 1", [0x02] = "VGA 2", [0x03] = "DVI 1", [0x04] = "DVI 2",
        [0x05] = "Composite 1", [0x06] = "Composite 2", [0x07] = "S-Video 1", [0x08] = "S-Video 2",
        [0x09] = "Tuner 1", [0x0A] = "Tuner 2", [0x0B] = "Tuner 3",
        [0x0C] = "Component 1", [0x0D] = "Component 2", [0x0E] = "Component 3",
        [0x0F] = "DisplayPort 1", [0x10] = "DisplayPort 2", [0x11] = "HDMI 1", [0x12] = "HDMI 2",
    };

    private static readonly Dictionary<int, string> ColorPresets = new()
    {
        [0x01] = "sRGB", [0x02] = "Native", [0x03] = "4000K", [0x04] = "5000K", [0x05] = "6500K",
        [0x06] = "7500K", [0x07] = "8200K", [0x08] = "9300K", [0x09] = "10000K", [0x0A] = "11500K",
        [0x0B] = "User 1", [0x0C] = "User 2", [0x0D] = "User 3",
    };

    private static readonly Dictionary<int, string> PowerModes = new()
    {
        [0x01] = "On", [0x02] = "Standby", [0x03] = "Suspend", [0x04] = "Off", [0x05] = "Off (power)",
    };
}
