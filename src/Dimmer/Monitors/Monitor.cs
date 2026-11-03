using Dimmer.Core;

namespace Dimmer.Monitors;

/// <summary>接続中の 1 台の Monitor。</summary>
/// <param name="Key">抜き差しや再起動をまたいで同じ Monitor を指す特定キー（<see cref="Core.MonitorIdentity.Key"/>）</param>
/// <param name="ModelName">型番名</param>
/// <param name="DisplayName">表示名（Monitor Name。未設定なら型番名＋番号）</param>
/// <param name="IsInternal">内蔵パネルか。内蔵パネルは WMI で操作する</param>
/// <param name="GdiDeviceName"><c>\\.\DISPLAY1</c> のような GDI のデバイス名。Orientation の変更に使う</param>
/// <param name="InstanceId"><c>DISPLAY\DEL4099\...</c> のようなデバイスインスタンス ID。WMI の InstanceName の接頭辞</param>
/// <param name="PhysicalMonitor">DDC/CI 用の物理モニターハンドル。無ければ 0</param>
/// <param name="IsControllable">DDC/CI に応答する、または内蔵パネルの明るさを WMI で操作できるか。false なら Orientation 以外は操作できない</param>
/// <param name="Settings">対応している Setting と選択肢（連続値は空）。capabilities で報告されていない Setting と値は含まない</param>
public sealed record Monitor(
    string Key,
    string ModelName,
    string DisplayName,
    bool IsInternal,
    string GdiDeviceName,
    string InstanceId,
    nint PhysicalMonitor,
    bool IsControllable,
    IReadOnlyDictionary<Setting, IReadOnlyList<int>> Settings);
