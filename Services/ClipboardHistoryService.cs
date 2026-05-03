using System.Security.Cryptography;
using System.Text;
using ClipNest.Data;
using ClipNest.Models;

namespace ClipNest.Services;

public sealed class ClipboardHistoryService(
    ClipboardRepository repository,
    SensitiveContentService sensitiveContent,
    SourceAppService sourceApp)
{
    private static readonly TimeSpan[] ClipboardRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(35),
        TimeSpan.FromMilliseconds(90),
        TimeSpan.FromMilliseconds(160)
    ];

    private string? _lastHash;

    public event EventHandler? Changed;

    public bool IsPaused { get; set; }

    public int HistoryLimit { get; set; } = 100;

    public async Task CaptureCurrentTextAsync()
    {
        if (IsPaused)
        {
            return;
        }

        var text = await TryReadClipboardTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (sensitiveContent.ShouldSkip(text))
        {
            return;
        }

        var hash = Hash(text);
        if (hash == _lastHash)
        {
            return;
        }

        _lastHash = hash;
        var now = DateTime.UtcNow;
        await repository.UpsertAsync(new ClipboardItem
        {
            ContentText = text,
            ContentHash = hash,
            SourceApp = sourceApp.GetSourceApp(),
            CreatedAt = now,
            UpdatedAt = now
        });
        await repository.TrimHistoryAsync(HistoryLimit);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static async Task<string?> TryReadClipboardTextAsync()
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < ClipboardRetryDelays.Length; attempt++)
        {
            var delay = ClipboardRetryDelays[attempt];
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            try
            {
                return System.Windows.Clipboard.ContainsText()
                    ? System.Windows.Clipboard.GetText()
                    : null;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError is not null)
        {
            AppLogger.Error(lastError, "读取剪切板文本失败，已完成短暂重试。");
        }

        return null;
    }
}
