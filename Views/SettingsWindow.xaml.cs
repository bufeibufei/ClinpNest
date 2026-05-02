using System.Windows;
using System.Windows.Input;
using ClipNest.Models;

namespace ClipNest.Views;

public partial class SettingsWindow
{
    private HotkeySettings _current;
    private bool _syncingLimit;

    public SettingsWindow(HotkeySettings current, int historyLimit)
    {
        _syncingLimit = true;
        InitializeComponent();
        _current = current;
        SelectedHotkey = current;
        HistoryLimit = ClampHistoryLimit(historyLimit);
        HotkeyBox.Text = current.DisplayText;
        HistoryLimitSlider.Value = HistoryLimit;
        HistoryLimitBox.Text = HistoryLimit.ToString();
        HintText.Text = "建议保留至少两个修饰键，避免和系统快捷键冲突。";
        _syncingLimit = false;
    }

    public HotkeySettings? SelectedHotkey { get; private set; }

    public int HistoryLimit { get; private set; }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;

        if (modifiers == HotkeyModifiers.None)
        {
            HintText.Text = "请至少包含 Ctrl、Alt、Shift 或 Win 中的一个。";
            return;
        }

        _current = new HotkeySettings { Modifiers = modifiers, Key = key };
        SelectedHotkey = _current;
        HotkeyBox.Text = _current.DisplayText;
        HintText.Text = "保存后立即生效。";
    }

    private void HistoryLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingLimit)
        {
            return;
        }

        SetHistoryLimit((int)e.NewValue);
    }

    private void HistoryLimitBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_syncingLimit || !int.TryParse(HistoryLimitBox.Text, out var value))
        {
            return;
        }

        SetHistoryLimit(value);
    }

    private void SetHistoryLimit(int value)
    {
        HistoryLimit = ClampHistoryLimit(value);
        if (HistoryLimitSlider is null || HistoryLimitBox is null)
        {
            return;
        }

        _syncingLimit = true;
        HistoryLimitSlider.Value = HistoryLimit;
        HistoryLimitBox.Text = HistoryLimit.ToString();
        _syncingLimit = false;
    }

    private static int ClampHistoryLimit(int value) => Math.Clamp(value, 20, 500);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedHotkey = _current;
        SetHistoryLimit(HistoryLimit);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
