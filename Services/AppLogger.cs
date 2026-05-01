namespace ClipNest.Services;

using System.IO;

public static class AppLogger
{
    private static readonly object Sync = new();

    public static string LogPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipNest");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "clipnest.log");
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(Exception exception, string context) => Write("ERROR", $"{context}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
    }
}
