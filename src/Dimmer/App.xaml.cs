using Dimmer.Monitors;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Dimmer;

public partial class App : Application
{
    private static readonly ResourceLoader Strings = new();

    private TaskbarIcon? _trayIcon;
    private MessageWindow? _messageWindow;

    public MonitorService Monitors { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // ウィンドウは持たず、トレイアイコンだけで常駐する
        var exit = new MenuFlyoutItem { Text = Strings.GetString("TrayExit") };
        exit.Click += (_, _) => ExitApp();

        var menu = new MenuFlyout();
        menu.Items.Add(exit);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = Strings.GetString("TrayToolTip"),
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/TrayIcon.ico")),
            ContextMenuMode = ContextMenuMode.SecondWindow,
            ContextFlyout = menu,
        };
        _trayIcon.ForceCreate();

        // 列挙は DDC/CI の応答待ちがあるので裏で行う
        _messageWindow = new MessageWindow();
        Monitors = new MonitorService(_messageWindow);
        Monitors.Changed += () =>
        {
            foreach (var m in Monitors.Monitors)
                Log.Write($"Monitor: {m.DisplayName} key={m.Key} internal={m.IsInternal} gdi={m.GdiDeviceName} controllable={m.IsControllable}");
        };
        Task.Run(Monitors.Refresh);
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
