using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ClipNest.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "clipnest.ico");
        _notifyIcon = new NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Text = "ClipNest",
            Visible = true
        };
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? TogglePauseRequested;
    public event EventHandler? ClearRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void BuildMenu(Func<bool> isPaused)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开 ClipNest", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(isPaused() ? "恢复记录" : "暂停记录", null, (_, _) => TogglePauseRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("清空历史", null, (_, _) => ClearRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowMessage(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(1500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
