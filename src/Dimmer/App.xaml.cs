using Dimmer.Core;
using Dimmer.Monitors;
using Dimmer.Profiles;
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

    public MonitorService Monitors { get; private set; } = null!;

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
    }

    /// <summary>Profile を Apply し、直近に Apply された Profile として保存する。スキップや失敗があればトーストで知らせる。</summary>
    /// <returns>スキップや失敗の一覧</returns>
    public async Task<List<string>> ApplyProfileAsync(Profile profile)
    {
        var problems = await ProfileApplier.ApplyAsync(profile, Monitors.Monitors, Settings.Settings.MonitorNames);
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
        Monitors.Dispose();
        _messageWindow?.Dispose();
        Exit();
    }
}
