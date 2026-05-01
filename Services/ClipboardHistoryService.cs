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
    private string? _lastHash;

    public event EventHandler? Changed;

    public bool IsPaused { get; set; }

    public async Task CaptureCurrentTextAsync()
    {
        if (IsPaused)
        {
            return;
        }

        string text;
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                return;
            }

            text = System.Windows.Clipboard.GetText();
        }
        catch
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

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
