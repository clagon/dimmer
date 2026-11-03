using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Dimmer;

/// <summary>エラーなどをトースト通知で知らせる。</summary>
public static class Notifier
{
    public static void Show(string title, string body)
    {
        Log.Write($"Notify: {title} / {body.Replace('\n', ' ')}");
        try
        {
            AppNotificationManager.Default.Show(new AppNotificationBuilder().AddText(title).AddText(body).BuildNotification());
        }
        catch (Exception e)
        {
            // 通知を出せなくてもアプリは止めない（ログには残る）
            Log.Write($"Notify failed: {e.Message}");
        }
    }
}
