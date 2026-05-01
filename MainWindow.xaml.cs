using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClipNest.Data;
using ClipNest.Models;
using ClipNest.Services;
using ClipNest.Views;

namespace ClipNest;

public partial class MainWindow
{
    private readonly ClipboardRepository _clipboardRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly ClipboardHistoryService _historyService;
    private readonly ClipboardMonitorService _monitorService;
    private readonly HotkeyService _hotkeyService;
    private readonly PasteService _pasteService;
    private readonly TrayService _trayService;
    private readonly ObservableCollection<ClipboardItem> _items = [];
    private HotkeySettings _hotkeySettings;
    private QuickPanelWindow? _quickPanel;
    private bool _favoritesOnly;
    private bool _isExiting;

    public MainWindow(
        ClipboardRepository clipboardRepository,
        SettingsRepository settingsRepository,
        ClipboardHistoryService historyService,
        ClipboardMonitorService monitorService,
        HotkeyService hotkeyService,
        PasteService pasteService,
        TrayService trayService,
        HotkeySettings hotkeySettings)
    {
        InitializeComponent();
        _clipboardRepository = clipboardRepository;
        _settingsRepository = settingsRepository;
        _historyService = historyService;
        _monitorService = monitorService;
        _hotkeyService = hotkeyService;
        _pasteService = pasteService;
        _trayService = trayService;
        _hotkeySettings = hotkeySettings;

        ItemsList.ItemsSource = _items;
        SearchBox.Text = string.Empty;
        UpdateStatus();

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        _historyService.Changed += async (_, _) => await Dispatcher.InvokeAsync(RefreshAsync);
        _hotkeyService.Pressed += (_, _) => Dispatcher.Invoke(ShowQuickPanel);

        _trayService.OpenRequested += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        _trayService.TogglePauseRequested += (_, _) => Dispatcher.Invoke(TogglePause);
        _trayService.SettingsRequested += (_, _) => Dispatcher.Invoke(OpenSettings);
        _trayService.ClearRequested += async (_, _) => await Dispatcher.InvokeAsync(ClearAsync);
        _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        _trayService.BuildMenu(() => _historyService.IsPaused);
    }

    private async void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _monitorService.Attach(helper);
        _hotkeyService.Attach(helper);
        TryRegisterHotkey(_hotkeySettings, showMessage: true);
        await RefreshAsync();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async Task RefreshAsync()
    {
        var results = await _clipboardRepository.SearchAsync(SearchBox.Text);
        _items.Clear();
        foreach (var item in _favoritesOnly ? results.Where(item => item.IsFavorite) : results)
        {
            _items.Add(item);
        }

        PageSubtitle.Text = $"{_items.Count} 条记录 · 快捷键 {_hotkeyService.Current.DisplayText}";
    }

    private void UpdateStatus()
    {
        PauseButton.Content = _historyService.IsPaused ? "恢复记录" : "暂停记录";
        StatusText.Text = _historyService.IsPaused ? "当前已暂停记录。" : "后台正在记录文本剪切板。";
        _trayService.BuildMenu(() => _historyService.IsPaused);
    }

    private void TryRegisterHotkey(HotkeySettings settings, bool showMessage)
    {
        try
        {
            _hotkeyService.Register(settings);
            _hotkeySettings = settings;
            if (showMessage)
            {
                _trayService.ShowMessage("ClipNest 已启动", $"快捷键：{settings.DisplayText}");
            }
        }
        catch (Exception ex)
        {
            _trayService.ShowMessage("快捷键注册失败", ex.Message);
        }
    }

    private void ShowQuickPanel()
    {
        _quickPanel ??= new QuickPanelWindow(_clipboardRepository, _pasteService);
        _quickPanel.Owner = this;
        _quickPanel.ShowPanel();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _ = RefreshAsync();
    }

    private void TogglePause()
    {
        _historyService.IsPaused = !_historyService.IsPaused;
        UpdateStatus();
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_hotkeySettings)
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.SelectedHotkey is null)
        {
            return;
        }

        TryRegisterHotkey(window.SelectedHotkey, showMessage: false);
        _ = _settingsRepository.SetAsync("quick_panel_hotkey", window.SelectedHotkey.ToString());
        _ = RefreshAsync();
    }

    private async Task ClearAsync()
    {
        await _clipboardRepository.ClearAsync();
        await RefreshAsync();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _quickPanel?.Close();
        _monitorService.Dispose();
        _hotkeyService.Dispose();
        _trayService.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void ClearButton_Click(object sender, RoutedEventArgs e) => await ClearAsync();

    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private async void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = false;
        PageTitle.Text = "历史";
        await RefreshAsync();
    }

    private async void FavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = true;
        PageTitle.Text = "收藏";
        await RefreshAsync();
    }

    private async void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is ClipboardItem item)
        {
            await _clipboardRepository.ToggleFavoriteAsync(item.Id);
            await RefreshAsync();
        }
    }

    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is ClipboardItem item)
        {
            await _clipboardRepository.SoftDeleteAsync(item.Id);
            await RefreshAsync();
        }
    }

    private async void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is ClipboardItem item)
        {
            await _pasteService.PasteAsync(item);
            await RefreshAsync();
        }
    }

    private async void ItemsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ItemsList.SelectedItem is ClipboardItem item)
        {
            await _pasteService.PasteAsync(item);
            await RefreshAsync();
        }
    }
}
