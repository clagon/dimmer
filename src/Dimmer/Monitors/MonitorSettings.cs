using System.Management;
using System.Text;
using Dimmer.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Dimmer.Monitors;

/// <summary>Setting の読み書きに失敗した。</summary>
public sealed class SettingException(string message) : Exception(message);

/// <summary>
/// Monitor の Setting の現在値を読み書きする。値は連続値なら 0〜100%、選択式なら VCP のコード値、Orientation なら度数。
/// キャッシュは持たず、毎回 Monitor に問い合わせる。
/// </summary>
public static class MonitorSettings
{
    private const int Attempts = 3; // 初回 + 再試行 2 回
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CommandInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan InputSwitchInterval = TimeSpan.FromSeconds(2.5);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, Lock> Gates = new();

    /// <summary>現在値を読む。</summary>
    /// <exception cref="SettingException">非対応、または Monitor が応答しない</exception>
    public static int Read(Monitor monitor, Setting setting)
    {
        EnsureSupported(monitor, setting);
        if (setting == Setting.Orientation) return ReadOrientation(monitor);
        if (monitor.IsInternal) return ReadInternalBrightness(monitor);

        var (current, max) = Retry(monitor, setting, () => ReadVcp(monitor.PhysicalMonitor, setting.VcpCode()!.Value));
        // 選択式は下位バイトが値（上位バイトに別の値を返す機種がある。例: DELL S2422HG の入力ソースは 0x0F0F）
        return setting.IsContinuous() ? Vcp.ToPercent(current, max) : (int)(current & 0xFF);
    }

    /// <summary>値を書き込む。失敗したら短い間隔で 2 回まで再試行する。</summary>
    /// <exception cref="SettingException">非対応の Setting や値、または再試行しても書き込めない</exception>
    public static void Write(Monitor monitor, Setting setting, int value)
    {
        var choices = EnsureSupported(monitor, setting);
        if (setting.IsContinuous() ? value is < 0 or > 100 : !choices.Contains(value))
            throw new SettingException($"{monitor.DisplayName}: {setting} は {value} に対応していません");

        if (setting == Setting.Orientation) { WriteOrientation(monitor, value); return; }
        if (monitor.IsInternal) { Retry(monitor, setting, () => WriteInternalBrightness(monitor, value)); return; }

        var code = setting.VcpCode()!.Value;
        Retry(monitor, setting, () =>
        {
            // 連続値は Monitor が報告する最大値で換算するので、先に最大値を読む
            var raw = setting.IsContinuous() ? Vcp.ToRaw(value, ReadVcp(monitor.PhysicalMonitor, code).Max) : (uint)value;
            // 入力ソースを書き込むと、同じ値でも入力を検出し直す間は応答しなくなる機種がある（DELL S2422HG で確認）
            return Ddc(monitor.PhysicalMonitor, () =>
                PInvoke.SetVCPFeature(new HANDLE(monitor.PhysicalMonitor), code, raw) != 0 ? true : throw new SettingException("SetVCPFeature failed"),
                setting == Setting.InputSource ? InputSwitchInterval : CommandInterval);
        });
    }

    /// <summary>capabilities 文字列を読む。応答しなければ null。取りこぼしがあるので 3 回まで試す。</summary>
    public static unsafe string? ReadCapabilities(nint physicalMonitor)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0) Thread.Sleep(RetryDelay);
            var handle = new HANDLE(physicalMonitor);
            uint length;
            if (PInvoke.GetCapabilitiesStringLength(handle, &length) == 0 || length == 0) continue;
            var buffer = new byte[length];
            fixed (byte* p = buffer)
            {
                if (PInvoke.CapabilitiesRequestAndCapabilitiesReply(handle, new PSTR(p), length) == 0) continue;
            }
            return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        }
        return null;
    }

    private static IReadOnlyList<int> EnsureSupported(Monitor monitor, Setting setting) =>
        monitor.Settings.TryGetValue(setting, out var choices)
            ? choices
            : throw new SettingException($"{monitor.DisplayName} は {setting} に対応していません");

    private static T Retry<T>(Monitor monitor, Setting setting, Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception e) when (e is SettingException or ManagementException && attempt < Attempts)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (Exception e) when (e is SettingException or ManagementException)
            {
                throw new SettingException($"{monitor.DisplayName}: {setting} を操作できませんでした（{e.Message}）");
            }
        }
    }

    private static unsafe (uint Current, uint Max) ReadVcp(nint physicalMonitor, byte code) =>
        Ddc(physicalMonitor, () =>
        {
            uint current, max;
            if (PInvoke.GetVCPFeatureAndVCPFeatureReply(new HANDLE(physicalMonitor), code, null, &current, &max) == 0)
                throw new SettingException("GetVCPFeatureAndVCPFeatureReply failed");
            return (current, max);
        });

    /// <summary>
    /// DDC/CI のコマンドを 1 台ずつ順に送り、送るたびに間を空ける。
    /// 間を空けずに続けて送ると、次のコマンドに応答しない機種がある（DELL P2314H で確認）。
    /// </summary>
    private static T Ddc<T>(nint physicalMonitor, Func<T> command, TimeSpan? interval = null)
    {
        // ponytail: ハンドルごとのロックは列挙し直しても消さない。数台分なので放っておく
        lock (Gates.GetOrAdd(physicalMonitor, _ => new Lock()))
        {
            try { return command(); }
            finally { Thread.Sleep(interval ?? CommandInterval); }
        }
    }

    private static int ReadInternalBrightness(Monitor monitor) =>
        Retry(monitor, Setting.Brightness, () =>
            WmiInstance(monitor, "WmiMonitorBrightness")["CurrentBrightness"] is byte b ? b : throw new SettingException("CurrentBrightness がありません"));

    private static bool WriteInternalBrightness(Monitor monitor, int percent)
    {
        using var methods = (ManagementObject)WmiInstance(monitor, "WmiMonitorBrightnessMethods");
        methods.InvokeMethod("WmiSetBrightness", [(uint)1, (byte)percent]);
        return true;
    }

    private static ManagementBaseObject WmiInstance(Monitor monitor, string className)
    {
        using var searcher = new ManagementObjectSearcher(@"root\WMI", $"SELECT * FROM {className}");
        return searcher.Get().Cast<ManagementBaseObject>()
            .FirstOrDefault(o => o["InstanceName"] is string n && n.StartsWith(monitor.InstanceId + "_", StringComparison.OrdinalIgnoreCase))
            ?? throw new SettingException($"{className} が見つかりません");
    }

    private static unsafe int ReadOrientation(Monitor monitor)
    {
        var mode = CurrentMode(monitor);
        return (int)mode.Anonymous1.Anonymous2.dmDisplayOrientation * 90;
    }

    /// <summary>Orientation は DDC/CI ではなく Windows の表示設定（ChangeDisplaySettingsEx）で変える。</summary>
    private static unsafe void WriteOrientation(Monitor monitor, int degrees)
    {
        var mode = CurrentMode(monitor);
        var current = (int)mode.Anonymous1.Anonymous2.dmDisplayOrientation * 90;
        if (current == degrees) return;

        // 縦横が入れ替わるときは解像度の幅と高さも入れ替える
        if ((current - degrees) % 180 != 0) (mode.dmPelsWidth, mode.dmPelsHeight) = (mode.dmPelsHeight, mode.dmPelsWidth);
        mode.Anonymous1.Anonymous2.dmDisplayOrientation = (DEVMODE_DISPLAY_ORIENTATION)(degrees / 90);
        mode.dmFields = DEVMODE_FIELD_FLAGS.DM_DISPLAYORIENTATION | DEVMODE_FIELD_FLAGS.DM_PELSWIDTH | DEVMODE_FIELD_FLAGS.DM_PELSHEIGHT;

        DISP_CHANGE result;
        fixed (char* name = monitor.GdiDeviceName)
            result = PInvoke.ChangeDisplaySettingsEx(name, &mode, HWND.Null, CDS_TYPE.CDS_UPDATEREGISTRY, null);
        if (result != DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
            throw new SettingException($"{monitor.DisplayName}: Orientation を変更できませんでした（{result}）");
    }

    private static unsafe DEVMODEW CurrentMode(Monitor monitor)
    {
        var mode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        if (!PInvoke.EnumDisplaySettings(monitor.GdiDeviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode))
            throw new SettingException($"{monitor.DisplayName}: 表示設定を読めませんでした");
        return mode;
    }
}
