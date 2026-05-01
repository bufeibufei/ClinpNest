using System.Windows.Interop;

namespace ClipNest.Services;

public sealed class ClipboardMonitorService(ClipboardHistoryService historyService) : IDisposable
{
    private HwndSource? _source;
    private IntPtr _hwnd;

    public void Attach(WindowInteropHelper helper)
    {
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(_hwnd);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmClipboardUpdate)
        {
            _ = historyService.CaptureCurrentTextAsync();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_hwnd);
        }
    }
}
