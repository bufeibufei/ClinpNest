using System.Windows.Forms;
using System.Runtime.InteropServices;
using ClipNest.Data;
using ClipNest.Models;

namespace ClipNest.Services;

public sealed class PasteService(ClipboardRepository repository)
{
    public async Task PasteAsync(ClipboardItem item)
    {
        var copied = await TrySetClipboardTextAsync(item.ContentText);
        if (!copied)
        {
            return;
        }

        await repository.MarkUsedAsync(item.Id);
        await Task.Delay(80);
        try
        {
            SendKeys.SendWait("^v");
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Send paste hotkey failed");
        }
    }

    private static async Task<bool> TrySetClipboardTextAsync(string text)
    {
        const int attempts = 8;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                System.Windows.Forms.Clipboard.SetDataObject(text, true, 5, 80);
                return true;
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x800401D0)
            {
                if (attempt == attempts)
                {
                    AppLogger.Error(ex, "OpenClipboard failed while pasting");
                    return false;
                }

                await Task.Delay(60 * attempt);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Set clipboard text failed while pasting");
                return false;
            }
        }

        return false;
    }
}
