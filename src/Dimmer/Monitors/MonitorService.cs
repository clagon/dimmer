using System.Management;
using Dimmer.Core;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Dimmer.Monitors;

/// <summary>
/// 接続中の Monitor を列挙し、抜き差し（WM_DISPLAYCHANGE）のたびに少し待ってから列挙し直す。
/// </summary>
public sealed class MonitorService : IDisposable
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private readonly Timer _debounce;
    private IReadOnlyDictionary<string, string> _userNames = new Dictionary<string, string>();

    public MonitorService(MessageWindow window)
    {
        _debounce = new Timer(_ => Refresh());
        window.MessageReceived += (msg, _, _) =>
        {
            // 抜き差しの直後は構成が落ち着いていないので、最後の通知から少し待って列挙し直す
            if (msg == PInvoke.WM_DISPLAYCHANGE) _debounce.Change(SettleDelay, Timeout.InfiniteTimeSpan);
        };
    }

    /// <summary>直近に列挙した Monitor。並びは Windows の接続パスの順。</summary>
    public IReadOnlyList<Monitor> Monitors { get; private set; } = [];

    /// <summary>列挙し直したとき（スレッドプールから呼ばれることがある）。</summary>
    public event Action? Changed;

    /// <summary>ユーザーが付けた Monitor Name（特定キー → 名前）を差し替え、表示名を付け直す。</summary>
    public void SetUserNames(IReadOnlyDictionary<string, string> userNames)
    {
        lock (_gate)
        {
            _userNames = userNames;
            Monitors = WithDisplayNames(Monitors, userNames);
        }
        Changed?.Invoke();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            var old = Monitors;
            Monitors = WithDisplayNames(Enumerate(), _userNames);
            // ponytail: 古いハンドルは即破棄。操作中の Setting の読み書きと競合したら、その操作が失敗するだけ
            foreach (var m in old) DestroyPhysicalMonitor(m.PhysicalMonitor);
        }
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _debounce.Dispose();
        lock (_gate)
        {
            foreach (var m in Monitors) DestroyPhysicalMonitor(m.PhysicalMonitor);
            Monitors = [];
        }
    }

    private static IReadOnlyList<Monitor> WithDisplayNames(IReadOnlyList<Monitor> monitors, IReadOnlyDictionary<string, string> userNames)
    {
        var names = MonitorIdentity.DisplayNames(monitors.Select(m => (m.Key, m.ModelName)).ToList(), userNames);
        return monitors.Select((m, i) => m with { DisplayName = names[i] }).ToList();
    }

    private static List<Monitor> Enumerate()
    {
        var physicalByGdi = PhysicalMonitorsByGdiName();
        var used = new Dictionary<string, int>();
        var panelIds = new Lazy<List<ManagementBaseObject>>(() => Wmi("SELECT * FROM WmiMonitorID"));
        var brightness = new Lazy<List<ManagementBaseObject>>(() => Wmi("SELECT InstanceName FROM WmiMonitorBrightness"));
        var monitors = new List<Monitor>();

        foreach (var path in ActivePaths())
        {
            var (devicePath, friendlyName, isInternal) = TargetName(path);
            var gdiName = SourceName(path);
            var instanceId = MonitorIdentity.InstanceIdFromDevicePath(devicePath);

            // 複製表示では 1 つの HMONITOR に複数の物理モニターがぶら下がる。接続パスの順に割り当てる
            // ponytail: 接続パスと物理モニターの順が一致する前提。ずれる機種に当たったら EDID で突き合わせる
            nint physical = 0;
            if (physicalByGdi.TryGetValue(gdiName, out var handles))
            {
                var i = used.GetValueOrDefault(gdiName);
                used[gdiName] = i + 1;
                if (i < handles.Count) { physical = handles[i]; handles[i] = 0; }
            }

            EdidInfo? edid;
            bool controllable;
            if (isInternal)
            {
                // 内蔵パネルは WMI で特定し、明るさを WMI で操作できるかで操作可否を決める
                var wmi = panelIds.Value.FirstOrDefault(o => IsInstance(o, instanceId));
                edid = wmi is null ? ReadEdid(instanceId) : EdidFromWmi(wmi);
                controllable = brightness.Value.Any(o => IsInstance(o, instanceId));
            }
            else
            {
                edid = ReadEdid(instanceId);
                controllable = physical != 0 && RespondsToDdcCi(physical);
            }

            var model = edid?.ModelName ?? (friendlyName.Length > 0 ? friendlyName : instanceId);
            monitors.Add(new Monitor(MonitorIdentity.Key(edid, devicePath), model, model, isInternal, gdiName, instanceId, physical, controllable));
        }

        // 割り当てなかった物理モニターハンドルは閉じる
        foreach (var h in physicalByGdi.Values.SelectMany(x => x)) DestroyPhysicalMonitor(h);
        return monitors;
    }

    /// <summary>DDC/CI に応答するか。capabilities 文字列の長さを問い合わせて確かめる。</summary>
    private static unsafe bool RespondsToDdcCi(nint physical)
    {
        // DDC/CI はモニターが忙しいと取りこぼすことがあるので、少し間を空けて 3 回まで試す
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) Thread.Sleep(100);
            uint length;
            if (PInvoke.GetCapabilitiesStringLength(new HANDLE(physical), &length) != 0) return true;
        }
        return false;
    }

    private static unsafe DISPLAYCONFIG_PATH_INFO[] ActivePaths()
    {
        while (true)
        {
            if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != WIN32_ERROR.ERROR_SUCCESS)
                return [];
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            WIN32_ERROR result;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
                result = PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, &pathCount, p, &modeCount, m, null);
            // サイズの取得と本体の取得の間に構成が変わるとバッファー不足になる。取り直す
            if (result == WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER) continue;
            return result == WIN32_ERROR.ERROR_SUCCESS ? paths[..(int)pathCount] : [];
        }
    }

    private static unsafe (string DevicePath, string FriendlyName, bool IsInternal) TargetName(DISPLAYCONFIG_PATH_INFO path)
    {
        var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
        target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
        target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
        target.header.adapterId = path.targetInfo.adapterId;
        target.header.id = path.targetInfo.id;
        PInvoke.DisplayConfigGetDeviceInfo(&target.header);

        var isInternal = target.outputTechnology is DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL
            or DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED
            or DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED;
        return (Str(target.monitorDevicePath.AsReadOnlySpan()), Str(target.monitorFriendlyDeviceName.AsReadOnlySpan()), isInternal);
    }

    private static unsafe string SourceName(DISPLAYCONFIG_PATH_INFO path)
    {
        var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
        source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
        source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
        source.header.adapterId = path.sourceInfo.adapterId;
        source.header.id = path.sourceInfo.id;
        PInvoke.DisplayConfigGetDeviceInfo(&source.header);
        return Str(source.viewGdiDeviceName.AsReadOnlySpan());
    }

    /// <summary>GDI デバイス名（<c>\\.\DISPLAY1</c>）ごとの DDC/CI 用の物理モニターハンドル。</summary>
    private static unsafe Dictionary<string, List<nint>> PhysicalMonitorsByGdiName()
    {
        var hmonitors = new List<HMONITOR>();
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (h, _, _, _) => { hmonitors.Add(h); return true; }, 0);

        var result = new Dictionary<string, List<nint>>();
        foreach (var h in hmonitors)
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            if (!PInvoke.GetMonitorInfo(h, (MONITORINFO*)&info)) continue;

            var handles = new List<nint>();
            if (PInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(h, out var count) && count > 0)
            {
                var physical = new PHYSICAL_MONITOR[count];
                fixed (PHYSICAL_MONITOR* p = physical)
                {
                    if (PInvoke.GetPhysicalMonitorsFromHMONITOR(h, count, p))
                        handles.AddRange(physical.Select(x => (nint)x.hPhysicalMonitor.Value));
                }
            }
            result[Str(info.szDevice.AsReadOnlySpan())] = handles;
        }
        return result;
    }

    private static unsafe void DestroyPhysicalMonitor(nint handle)
    {
        if (handle == 0) return;
        var m = new PHYSICAL_MONITOR { hPhysicalMonitor = new HANDLE(handle) };
        PInvoke.DestroyPhysicalMonitors(1, &m);
    }

    private static EdidInfo? ReadEdid(string instanceId)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{instanceId}\Device Parameters");
        return key?.GetValue("EDID") is byte[] edid ? Edid.Parse(edid) : null;
    }

    private static EdidInfo EdidFromWmi(ManagementBaseObject o)
    {
        static string? Text(ManagementBaseObject o, string name) =>
            o[name] is ushort[] chars && new string(chars.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim() is { Length: > 0 } s ? s : null;
        return new EdidInfo(Text(o, "ManufacturerName") ?? "", (Text(o, "ProductCodeID") ?? "").ToUpperInvariant(), Text(o, "SerialNumberID"), Text(o, "UserFriendlyName"));
    }

    private static bool IsInstance(ManagementBaseObject o, string instanceId) =>
        o["InstanceName"] is string name && name.StartsWith(instanceId + "_", StringComparison.OrdinalIgnoreCase);

    private static List<ManagementBaseObject> Wmi(string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", query);
            return searcher.Get().Cast<ManagementBaseObject>().ToList();
        }
        catch (ManagementException)
        {
            // そのクラスのインスタンスが無い環境（明るさを WMI で操作できない PC など）
            return [];
        }
    }

    private static string Str(ReadOnlySpan<char> s) => (s.IndexOf('\0') is var i and >= 0 ? s[..i] : s).ToString();
}
