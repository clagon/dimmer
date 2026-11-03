using Windows.Storage;

namespace Dimmer;

/// <summary>アプリデータ領域の dimmer.log に 1 行ずつ追記する。不具合調査用。</summary>
public static class Log
{
    private static readonly string FilePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "dimmer.log");
    private static readonly Lock Gate = new();

    public static void Write(string message)
    {
        // ponytail: ローテーションなし。肥大化したら起動時に切り詰める
        lock (Gate)
        {
            try { File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}"); }
            catch (IOException) { }
        }
    }
}
