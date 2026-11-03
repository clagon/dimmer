using Dimmer.Adjustments;
using Dimmer.Core;
using Dimmer.Monitors;
using Dimmer.Overlay;
using Dimmer.Profiles;
using Dimmer.Shortcuts;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AppLifecycle;
using Windows.Storage;

namespace Dimmer;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MessageWindow? _messageWindow;
    private readonly SemaphoreSlim _applyGate = new(1, 1);

    public MonitorService Monitors { get; private set; } = null!;

    public ShortcutBindings Shortcuts { get; private set; } = null!;

    private HotkeyService? _hotkeys;
    private OverlayWindow? _overlay;
    private int _overlayRequest;
    private AdjustmentRunner? _adjustments;

    public SettingsFile Settings { get; } = new(Path.Combine(ApplicationData.Current.LocalFolder.Path, "settings.json"));

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Settings.Load() is { } error)
            Notifier.Show(Text.Get("SettingsLoadFailedTitle"), Text.Format("SettingsLoadFailedBody", error));

        var invalidAdjustments = Settings.Settings.Adjustments.Where(a => !a.IsValid).ToList();
        if (invalidAdjustments.Count > 0)
            Notifier.Show(Text.Get("AdjustmentInvalidTitle"),
                string.Join("\n", invalidAdjustments.Select(a => $"{Text.Adjustment(a, Settings.Settings.MonitorNames)}: {Text.Get("AdjustmentInvalid")}")));

        // 存在しない Profile は Cycle から取り除き、使えない Cycle を知らせる（Shortcut は登録しない）
        Cycles.RemoveMissingProfiles(Settings.Settings);
        var invalidCycles = Settings.Settings.Cycles.Where(c => Cycles.Problem(c) != CycleProblem.None).ToList();
        if (invalidCycles.Count > 0)
            Notifier.Show(Text.Get("CycleInvalidTitle"),
                string.Join("\n", invalidCycles.Select(c => Text.Format("CycleInvalidLine", c.Name, Text.Get($"CycleProblem_{Cycles.Problem(c)}")))));

        // ウィンドウは持たず、トレイアイコンだけで常駐する
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = Text.Get("TrayToolTip"),
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/TrayIcon.ico")),
            ContextMenuMode = ContextMenuMode.SecondWindow,
        };
        RebuildTrayMenu();
        _trayIcon.ForceCreate();

        // 列挙は DDC/CI の応答待ちがあるので裏で行う
        _messageWindow = new MessageWindow();
        Monitors = new MonitorService(_messageWindow);
        Monitors.SetUserNames(Settings.Settings.MonitorNames);
        Monitors.Changed += () =>
        {
            foreach (var m in Monitors.Monitors)
                Log.Write($"Monitor: {m.DisplayName} key={m.Key} internal={m.IsInternal} gdi={m.GdiDeviceName} controllable={m.IsControllable} " +
                    $"settings=[{string.Join(", ", m.Settings.Select(s => s.Value.Count == 0 ? $"{s.Key}" : $"{s.Key}({string.Join("/", s.Value.Select(v => Vcp.ValueName(s.Key, v)))})"))}]");
        };
        Task.Run(Monitors.Refresh);

        _adjustments = new AdjustmentRunner(Monitors, () => Settings.Settings.MonitorNames, ShowOverlay, (a, failed) =>
            Notifier.Show(Text.Format("AdjustmentFailedTitle", Text.Adjustment(a, Settings.Settings.MonitorNames)),
                Text.Format("AdjustmentFailedBody", string.Join("、", failed))));

        _hotkeys = new HotkeyService(_messageWindow);
        Shortcuts = new ShortcutBindings(_hotkeys);
        var failed = BindShortcuts();
        if (failed.Count > 0)
            Notifier.Show(Text.Get("ShortcutFailedTitle"),
                string.Join("\n", failed.Select(f => Text.Format("ShortcutFailedLine", f.TargetName, f.Shortcut, f.Reason))));
    }

    /// <summary>設定ファイルの Shortcut を登録し直す。登録できなかったものを返す。</summary>
    public IReadOnlyList<FailedShortcut> BindShortcuts()
    {
        // Profile の Apply と Profile Cycle はキーリピートを無視し、1 回押すと 1 回だけ実行する
        var profiles = Settings.Settings.Profiles.Select(p => new ShortcutTarget(p.Name, p.Shortcut ?? "", isRepeat =>
        {
            if (!isRepeat) _ = ApplyProfileAsync(p, overlay: true);
        }));
        var cycles = Settings.Settings.Cycles.Where(c => Cycles.Problem(c) == CycleProblem.None).Select(c => new ShortcutTarget(c.Name, c.Shortcut ?? "", isRepeat =>
        {
            if (!isRepeat) _ = ApplyNextInCycleAsync(c);
        }));
        // Adjustment は自前の間隔で繰り返すので、キーリピートの通知は無視して離されるまで続ける
        var adjustments = Settings.Settings.Adjustments.Where(a => a.IsValid).Select(a => new ShortcutTarget(
            Text.Adjustment(a, Settings.Settings.MonitorNames), a.Shortcut ?? "",
            isRepeat => { if (!isRepeat) _adjustments!.Press(a); },
            () => _adjustments!.Release(a)));
        return Shortcuts.Bind(profiles.Concat(cycles).Concat(adjustments));
    }

    /// <summary>Profile Cycle の次の Profile を Apply し、Overlay に Profile 名と並びの中の位置（例: 2/3）を出す。</summary>
    public Task<List<string>> ApplyNextInCycleAsync(ProfileCycle cycle)
    {
        var i = Cycles.NextIndex(cycle.ProfileIds, Settings.Settings.LastAppliedProfileId);
        var profile = Settings.Settings.Profiles.First(p => p.Id == cycle.ProfileIds[i]);
        return ApplyProfileAsync(profile, overlay: true, position: $"{i + 1}/{cycle.ProfileIds.Count}");
    }

    /// <summary>Profile を Apply し、直近に Apply された Profile として保存する。スキップや失敗があればトーストで知らせる。</summary>
    /// <param name="overlay">Overlay に Profile 名を出すか（Shortcut から実行したとき）</param>
    /// <param name="position">Overlay に一緒に出す、Profile Cycle の中の位置（例: 2/3）</param>
    /// <returns>スキップや失敗の一覧</returns>
    public async Task<List<string>> ApplyProfileAsync(Profile profile, bool overlay = false, string? position = null)
    {
        Log.Write($"Apply: {profile.Name}");
        // 前の Apply が終わる前に Cycle の Shortcut が押されても次へ進むよう、待つ前に「直近」として記録する
        Settings.Settings.LastAppliedProfileId = profile.Id;
        // Overlay は共有なので、後から実行した Shortcut の表示を古い Apply の結果で上書きしないよう、最新の要求だけが更新する
        if (overlay) ShowOverlay(profile.Name, position);
        var overlayRequest = _overlayRequest;

        // 前の Apply の書き込みや再試行が、後から選んだ Profile を上書きしないよう 1 つずつ順に行う
        await Monitors.Ready;
        await _applyGate.WaitAsync();
        List<string> problems;
        try
        {
            // Apply 中に列挙し直しが起きても、使っている物理モニターハンドルを破棄させない
            using var lease = Monitors.Lease();
            problems = await ProfileApplier.ApplyAsync(profile, Monitors.Monitors, Settings.Settings.MonitorNames);
        }
        finally { _applyGate.Release(); }
        Settings.Save();

        if (problems.Count > 0)
        {
            // 詳しい内容はトーストに任せ、Overlay には一部を適用できなかったことだけを出す
            if (overlay && overlayRequest == _overlayRequest) ShowOverlay(profile.Name, position is null ? Text.Get("OverlayPartialFailure") : $"{position}  {Text.Get("OverlayPartialFailure")}");
            Notifier.Show(Text.Format("ApplyProblemsTitle", profile.Name), string.Join("\n", problems));
        }
        return problems;
    }

    /// <summary>Overlay を出す（設定で無効なら何もしない）。引数は <see cref="OverlayWindow.Show"/> と同じ。</summary>
    public void ShowOverlay(string title, string? detail = null, int? percent = null, nint monitor = 0)
    {
        // Adjustment など他の表示が後から出たら、実行中の Apply の結果で上書きしない
        _overlayRequest++;
        if (!Settings.Settings.OverlayEnabled) return;
        (_overlay ??= new OverlayWindow()).Show(title, detail, percent, monitor);
    }

    /// <summary>
    /// トレイの右クリックメニューを作り直す。Profile の一覧（設定ファイルの順）と「終了」。
    /// 直近に Apply された Profile に印は付けない（実際の状態と一致する保証がないため）。
    /// </summary>
    public void RebuildTrayMenu()
    {
        var menu = new MenuFlyout();
        foreach (var profile in Settings.Settings.Profiles)
        {
            var item = new MenuFlyoutItem { Text = profile.Name };
            item.Click += (_, _) => _ = ApplyProfileAsync(profile);
            menu.Items.Add(item);
        }
        if (menu.Items.Count > 0) menu.Items.Add(new MenuFlyoutSeparator());

        var exit = new MenuFlyoutItem { Text = Text.Get("TrayExit") };
        exit.Click += (_, _) => ExitApp();
        menu.Items.Add(exit);

        _trayIcon!.ContextFlyout = menu;
    }

    /// <summary>2 つ目のインスタンスから起動が転送されたときに呼ばれる（UI スレッド外）。</summary>
    public void OnRedirectedActivation(AppActivationArguments args)
    {
        // TODO(#9): 設定画面を開く
    }

    private void ExitApp()
    {
        _trayIcon?.Dispose();
        _hotkeys?.Dispose();
        Monitors.Dispose();
        _messageWindow?.Dispose();
        Exit();
    }
}
