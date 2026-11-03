using Dimmer.Core;
using Dimmer.Monitors;
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

    public SettingsFile Settings { get; } = new(Path.Combine(ApplicationData.Current.LocalFolder.Path, "settings.json"));

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Settings.Load() is { } error)
            Notifier.Show(Text.Get("SettingsLoadFailedTitle"), Text.Format("SettingsLoadFailedBody", error));

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

        _hotkeys = new HotkeyService(_messageWindow);
        Shortcuts = new ShortcutBindings(_hotkeys);
        var failed = BindShortcuts();
        if (failed.Count > 0)
            Notifier.Show(Text.Get("ShortcutFailedTitle"),
                string.Join("\n", failed.Select(f => Text.Format("ShortcutFailedLine", f.TargetName, f.Shortcut, f.Reason))));
    }

    /// <summary>設定ファイルの Shortcut を登録し直す。登録できなかったものを返す。</summary>
    public IReadOnlyList<FailedShortcut> BindShortcuts() =>
        Shortcuts.Bind(Settings.Settings.Profiles.Select(p => new ShortcutTarget(p.Name, p.Shortcut ?? "", isRepeat =>
        {
            // Profile の Apply はキーリピートを無視し、1 回押すと 1 回だけ実行する
            if (!isRepeat) _ = ApplyProfileAsync(p);
        })));

    /// <summary>Profile を Apply し、直近に Apply された Profile として保存する。スキップや失敗があればトーストで知らせる。</summary>
    /// <returns>スキップや失敗の一覧</returns>
    public async Task<List<string>> ApplyProfileAsync(Profile profile)
    {
        Log.Write($"Apply: {profile.Name}");
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
        Settings.Settings.LastAppliedProfileId = profile.Id;
        Settings.Save();
        if (problems.Count > 0) Notifier.Show(Text.Format("ApplyProblemsTitle", profile.Name), string.Join("\n", problems));
        return problems;
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
