using System.Windows.Forms;
using ClipNest.Data;
using ClipNest.Models;

namespace ClipNest.Services;

public sealed class PasteService(ClipboardRepository repository)
{
    public async Task PasteAsync(ClipboardItem item)
    {
        System.Windows.Clipboard.SetText(item.ContentText);
        await repository.MarkUsedAsync(item.Id);
        await Task.Delay(80);
        SendKeys.SendWait("^v");
    }
}
