using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using ClipNest.Data;
using ClipNest.Models;
using ClipNest.Services;
using ClipNest.Views;

namespace ClipNest;

public partial class MainWindow
{
    public static readonly DependencyProperty IsFavoritesViewProperty =
        DependencyProperty.Register(nameof(IsFavoritesView), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty CardWidthProperty =
        DependencyProperty.Register(nameof(CardWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(260d));

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
    private bool _isDragging;
    private bool _favoritesOnly;
    private bool _isExiting;
    private int _historyLimit = 100;
    private int _gridColumns;

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
        _historyLimit = _historyService.HistoryLimit;

        ItemsList.ItemsSource = _items;
        SearchBox.Text = string.Empty;
        SetPageMode(favoritesOnly: false);
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

    public bool IsFavoritesView
    {
        get => (bool)GetValue(IsFavoritesViewProperty);
        set => SetValue(IsFavoritesViewProperty, value);
    }

    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    private async void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _monitorService.Attach(helper);
        _hotkeyService.Attach(helper);
        TryRegisterHotkey(_hotkeySettings, showMessage: true);
        await LoadCategoriesAsync();
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
        var selectedCategory = SelectedCategory();
        if (!string.IsNullOrWhiteSpace(selectedCategory))
        {
            results = results.Where(item => string.Equals(item.FavoriteTag, selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _items.Clear();
        foreach (var item in results)
        {
            _items.Add(item);
        }

        var historyCount = await _clipboardRepository.CountActiveAsync();
        PageSubtitle.Text = $"{_items.Count} 条记录 · 快捷键 {_hotkeyService.Current.DisplayText}";
        SummaryTitleText.Text = $"{historyCount} / {_historyLimit}";
        HistoryUsageBar.Maximum = Math.Max(1, _historyLimit);
        HistoryUsageBar.Value = Math.Min(historyCount, _historyLimit);
    }

    private async Task LoadCategoriesAsync()
    {
        var previous = CategoryFilterBox.SelectedItem as string;
        CategoryFilterBox.Items.Clear();
        CategoryFilterBox.Items.Add("全部分类");
        foreach (var tag in await _clipboardRepository.GetFavoriteTagsAsync())
        {
            CategoryFilterBox.Items.Add(tag);
        }

        CategoryFilterBox.SelectedItem = !string.IsNullOrWhiteSpace(previous) && CategoryFilterBox.Items.Contains(previous)
            ? previous
            : "全部分类";
    }

    private string? SelectedCategory()
    {
        var selected = CategoryFilterBox.SelectedItem as string;
        return string.IsNullOrWhiteSpace(selected) || selected == "全部分类" ? null : selected;
    }

    private void UpdateStatus()
    {
        PauseButton.Content = _historyService.IsPaused ? "恢复记录" : "暂停记录";
        StatusText.Text = _historyService.IsPaused ? "当前已暂停记录。" : (_favoritesOnly ? "总剪切板历史" : "剪切板历史");
        _trayService.BuildMenu(() => _historyService.IsPaused);
    }

    private void SetPageMode(bool favoritesOnly)
    {
        _favoritesOnly = favoritesOnly;
        IsFavoritesView = favoritesOnly;
        PageTitle.Text = favoritesOnly ? "收藏" : "历史";
        PageIconText.Text = favoritesOnly ? "\uE734" : "\uE81C";
        ClearButtonText.Text = favoritesOnly ? "清空收藏" : "清空";
        StatusText.Text = _historyService.IsPaused ? "当前已暂停记录。" : (favoritesOnly ? "总剪切板历史" : "剪切板历史");

        CategoryColumn.Width = favoritesOnly ? new GridLength(190) : new GridLength(0);
        CategoryFilterBox.Visibility = favoritesOnly ? Visibility.Visible : Visibility.Collapsed;
        PaginationBar.Visibility = favoritesOnly ? Visibility.Visible : Visibility.Collapsed;

        var selectedBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 240, 255));
        var transparent = new SolidColorBrush(System.Windows.Media.Colors.Transparent);
        HistoryButton.Background = favoritesOnly ? transparent : selectedBackground;
        FavoritesButton.Background = favoritesOnly ? selectedBackground : transparent;
        PageIconBadge.Background = favoritesOnly
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 244, 218))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 243, 255));
        PageIconText.Foreground = favoritesOnly
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 125, 246));
        UpdateCardWidth();
    }

    private void UpdateCardWidth()
    {
        if (ItemsList is null)
        {
            return;
        }

        var availableWidth = ItemsList.ActualWidth;
        if (availableWidth <= 0)
        {
            return;
        }

        const double gap = 12;
        const double idealCardWidth = 224;
        const double minCardWidth = 206;
        var maxColumns = Math.Clamp((int)Math.Floor((availableWidth + gap) / (minCardWidth + gap)), 1, 5);
        var idealColumns = Math.Clamp((int)Math.Round((availableWidth + gap) / (idealCardWidth + gap)), 1, 5);
        var columns = Math.Min(maxColumns, idealColumns);

        if (_gridColumns > 0 && Math.Abs(columns - _gridColumns) == 1)
        {
            var currentWidth = (availableWidth - gap * (_gridColumns - 1)) / _gridColumns;
            var proposedWidth = (availableWidth - gap * (columns - 1)) / columns;
            if (currentWidth is >= 206 and <= 264 && proposedWidth is >= 206 and <= 264)
            {
                columns = _gridColumns;
            }
        }

        _gridColumns = columns;
        CardWidth = Math.Floor((availableWidth - gap * (columns - 1)) / columns);
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
        if (_quickPanel is null)
        {
            _quickPanel = new QuickPanelWindow(_clipboardRepository, _pasteService);
            _quickPanel.Closed += (_, _) => _quickPanel = null;
        }

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
        var window = new SettingsWindow(_hotkeySettings, _historyLimit)
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.SelectedHotkey is null)
        {
            return;
        }

        TryRegisterHotkey(window.SelectedHotkey, showMessage: false);
        _historyLimit = window.HistoryLimit;
        _historyService.HistoryLimit = _historyLimit;
        _ = _settingsRepository.SetAsync("quick_panel_hotkey", window.SelectedHotkey.ToString());
        _ = _settingsRepository.SetAsync("history_limit", _historyLimit.ToString());
        _ = _clipboardRepository.TrimHistoryAsync(_historyLimit);
        _ = RefreshAsync();
    }

    private async Task ClearAsync()
    {
        if (_favoritesOnly)
        {
            await _clipboardRepository.ClearFavoritesAsync();
            await LoadCategoriesAsync();
        }
        else
        {
            await _clipboardRepository.ClearAsync();
        }

        await RefreshAsync();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _quickPanel?.ForceClose();
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

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => await RefreshAsync();

    private async void CategoryFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void ClearButton_Click(object sender, RoutedEventArgs e) => await ClearAsync();

    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void ItemsList_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCardWidth();

    private async void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        SetPageMode(favoritesOnly: false);
        await RefreshAsync();
    }

    private async void FavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        SetPageMode(favoritesOnly: true);
        await RefreshAsync();
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

        await LoadCategoriesAsync();
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
        _isDragging = false;
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
            _isDragging = true;
            AnimateCard(card, 0.94, 0.58);
            System.Windows.DragDrop.DoDragDrop(card, _draggedItem, System.Windows.DragDropEffects.Move);
            AnimateCard(card, 1, 1);
        }
    }

    private async void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging || IsActionElement(e.OriginalSource as DependencyObject))
        {
            _isDragging = false;
            return;
        }

        if ((sender as FrameworkElement)?.Tag is ClipboardItem item)
        {
            await PasteItemAsync(item);
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
        _isDragging = false;
    }

    private static void AnimateCard(Border card, double scale, double opacity)
    {
        if (card.RenderTransform is not ScaleTransform transform || transform.IsFrozen)
        {
            transform = new ScaleTransform(1, 1);
            card.RenderTransform = transform;
        }

        var duration = TimeSpan.FromMilliseconds(120);
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration));
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration));
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, duration));
    }

    private static bool IsActionElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = source is Visual or Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }
}
