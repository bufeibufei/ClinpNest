using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private ClipboardItem? _draggedItem;
    private System.Windows.Point _dragStartPoint;
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
        var results = await _clipboardRepository.SearchAsync(SearchBox.Text, favoritesOnly: _favoritesOnly);
        _items.Clear();
        foreach (var item in results)
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

    private async Task PasteItemAsync(ClipboardItem item)
    {
        await _pasteService.PasteAsync(item);
        await RefreshAsync();
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

    private async void PasteItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is ClipboardItem item)
        {
            await PasteItemAsync(item);
        }
    }

    private async void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is not ClipboardItem item)
        {
            return;
        }

        var tags = await _clipboardRepository.GetFavoriteTagsAsync();
        var dialog = new FavoriteDialog(item, tags) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.RemoveFavorite)
        {
            await _clipboardRepository.UnfavoriteAsync(item.Id);
        }
        else
        {
            await _clipboardRepository.SetFavoriteAsync(item.Id, dialog.Alias, dialog.FavoriteTag);
        }

        await RefreshAsync();
    }

    private async void PinItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is ClipboardItem item)
        {
            await _clipboardRepository.SetPinnedAsync(item.Id, !item.IsPinned);
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

    private void ItemCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedItem = (sender as FrameworkElement)?.Tag as ClipboardItem;
    }

    private void ItemCard_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_favoritesOnly || _draggedItem is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is Border card)
        {
            AnimateCard(card, 0.94, 0.58);
            System.Windows.DragDrop.DoDragDrop(card, _draggedItem, System.Windows.DragDropEffects.Move);
            AnimateCard(card, 1, 1);
        }
    }

    private void ItemCard_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (_favoritesOnly && sender is Border card)
        {
            AnimateCard(card, 1.035, 1);
            card.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
        }
    }

    private void ItemCard_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border card)
        {
            AnimateCard(card, 1, 1);
            card.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrushSoft");
        }
    }

    private void ItemCard_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = _favoritesOnly && e.Data.GetDataPresent(typeof(ClipboardItem))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void ItemCard_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!_favoritesOnly || e.Data.GetData(typeof(ClipboardItem)) is not ClipboardItem source ||
            (sender as FrameworkElement)?.Tag is not ClipboardItem target ||
            source.Id == target.Id)
        {
            return;
        }

        var sourceItem = _items.FirstOrDefault(item => item.Id == source.Id);
        var targetItem = _items.FirstOrDefault(item => item.Id == target.Id);
        if (sourceItem is null || targetItem is null)
        {
            return;
        }

        _items.Move(_items.IndexOf(sourceItem), _items.IndexOf(targetItem));
        await _clipboardRepository.UpdateFavoriteOrderAsync(_items.ToList());
        await RefreshAsync();
    }

    private void ItemsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        _draggedItem = null;
    }

    private static void AnimateCard(Border card, double scale, double opacity)
    {
        if (card.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1, 1);
            card.RenderTransform = transform;
        }

        var duration = TimeSpan.FromMilliseconds(120);
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration));
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration));
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, duration));
    }
}
