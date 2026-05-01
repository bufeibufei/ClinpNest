using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using ClipNest.Models;

namespace ClipNest.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    public event EventHandler? Pressed;

    public HotkeySettings Current { get; private set; } = new();

    public void Attach(WindowInteropHelper helper)
    {
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public void Register(HotkeySettings settings)
    {
        Unregister();
        var virtualKey = KeyInterop.VirtualKeyFromKey(settings.Key);
        if (virtualKey == 0)
        {
            throw new InvalidOperationException("快捷键主键无效。");
        }

        if (!NativeMethods.RegisterHotKey(_hwnd, HotkeyId, (uint)settings.Modifiers, (uint)virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "快捷键被占用或注册失败。");
        }

        Current = settings;
        _registered = true;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
    }
}
