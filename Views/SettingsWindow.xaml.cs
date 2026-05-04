using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipNest.Data;
using ClipNest.Models;

namespace ClipNest.Views;

public partial class SettingsWindow
{
    private readonly ClipboardRepository _clipboardRepository;
    private readonly ObservableCollection<string> _categories = [];
    private HotkeySettings _current;
    private bool _syncingLimit;
    private bool _syncingCategory;

    public SettingsWindow(HotkeySettings current, int historyLimit, ClipboardRepository clipboardRepository, bool startWithWindows)
    {
        _syncingLimit = true;
        InitializeComponent();
        _clipboardRepository = clipboardRepository;
        _current = current;
        SelectedHotkey = current;
        HistoryLimit = ClampHistoryLimit(historyLimit);
        HotkeyBox.Text = current.DisplayText;
        HistoryLimitSlider.Value = HistoryLimit;
        HistoryLimitBox.Text = HistoryLimit.ToString();
        StartupCheckBox.IsChecked = startWithWindows;
        HintText.Text = "建议保留至少两个修饰键，避免和系统快捷键冲突。";
        CategoryList.ItemsSource = _categories;
        Loaded += async (_, _) => await LoadCategoriesAsync();
        _syncingLimit = false;
    }

    public HotkeySettings? SelectedHotkey { get; private set; }

    public int HistoryLimit { get; private set; }

    public bool CategoriesChanged { get; private set; }

    public bool StartWithWindows => StartupCheckBox.IsChecked == true;

    private async Task LoadCategoriesAsync(string? selected = null)
    {
        var previous = selected ?? CategoryList.SelectedItem as string;
        _syncingCategory = true;
        _categories.Clear();
        foreach (var tag in await _clipboardRepository.GetFavoriteTagsAsync())
        {
            _categories.Add(tag);
        }

        CategoryList.SelectedItem = !string.IsNullOrWhiteSpace(previous) && _categories.Contains(previous)
            ? previous
            : null;
        CategoryNameBox.Text = CategoryList.SelectedItem as string ?? string.Empty;
        _syncingCategory = false;
    }

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

    private void HistoryLimitBox_TextChanged(object sender, TextChangedEventArgs e)
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

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCategory)
        {
            return;
        }

        CategoryNameBox.Text = CategoryList.SelectedItem as string ?? string.Empty;
    }

    private async void SaveCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var newName = CategoryNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        if (CategoryList.SelectedItem is string oldName && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            await _clipboardRepository.RenameFavoriteTagAsync(oldName, newName);
        }
        else
        {
            await _clipboardRepository.AddFavoriteTagAsync(newName);
        }

        CategoriesChanged = true;
        await LoadCategoriesAsync(newName);
    }

    private async void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not string tag)
        {
            return;
        }

        await _clipboardRepository.DeleteFavoriteTagAsync(tag);
        CategoriesChanged = true;
        await LoadCategoriesAsync();
    }

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
