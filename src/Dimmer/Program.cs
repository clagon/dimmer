using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Dimmer;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey("Dimmer");
        if (!mainInstance.IsCurrent)
        {
            // 既に常駐しているインスタンスへ起動を渡して終了する。
            // STA スレッドで待つとデッドロックしうるため、別スレッドで待つ。
            var args = AppInstance.GetCurrent().GetActivatedEventArgs();
            // 1 つ目が設定画面を前面に出せるよう、前面に出す権利を渡す
            Windows.Win32.PInvoke.AllowSetForegroundWindow(mainInstance.ProcessId);
            Task.Run(() => mainInstance.RedirectActivationToAsync(args).AsTask()).Wait();
            return;
        }

        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            var app = new App();
            mainInstance.Activated += (_, e) => app.OnRedirectedActivation(e);
        });
    }
}
