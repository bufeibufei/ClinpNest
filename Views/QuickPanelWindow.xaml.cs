using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using ClipNest.Data;
using ClipNest.Models;
using ClipNest.Services;

namespace ClipNest.Views;

public partial class QuickPanelWindow
{
    public static readonly DependencyProperty FavoriteCardWidthProperty =
        DependencyProperty.Register(nameof(FavoriteCardWidth), typeof(double), typeof(QuickPanelWindow), new PropertyMetadata(220d));

    public static readonly DependencyProperty HistoryCardWidthProperty =
        DependencyProperty.Register(nameof(HistoryCardWidth), typeof(double), typeof(QuickPanelWindow), new PropertyMetadata(220d));

    public static readonly DependencyProperty SearchQueryProperty =
        DependencyProperty.Register(nameof(SearchQuery), typeof(string), typeof(QuickPanelWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SelectedItemIdProperty =
        DependencyProperty.Register(nameof(SelectedItemId), typeof(long), typeof(QuickPanelWindow), new PropertyMetadata(0L));

    private readonly ClipboardRepository _clipboardRepository;
    private readonly PasteService _pasteService;
    private readonly ObservableCollection<ClipboardItem> _favorites = [];
    private readonly ObservableCollection<ClipboardItem> _history = [];
    private readonly DispatcherTimer _searchDebounceTimer;
    private bool _forceClose;
    private int _favoriteColumns;
    private int _historyColumns;
    private double _lastFavoriteCardWidth;
    private double _lastHistoryCardWidth;
    private int _selectedIndex;

    public QuickPanelWindow(ClipboardRepository clipboardRepository, PasteService pasteService)
    {
        InitializeComponent();
        _clipboardRepository = clipboardRepository;
        _pasteService = pasteService;
        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchDebounceTimer.Tick += async (_, _) =>
        {
            _searchDebounceTimer.Stop();
            await RefreshAsync();
        };
        FavoritesList.ItemsSource = _favorites;
        HistoryList.ItemsSource = _history;
        Closing += QuickPanelWindow_Closing;
    }

    public double FavoriteCardWidth
    {
        get => (double)GetValue(FavoriteCardWidthProperty);
        set => SetValue(FavoriteCardWidthProperty, value);
    }

    public double HistoryCardWidth
    {
        get => (double)GetValue(HistoryCardWidthProperty);
        set => SetValue(HistoryCardWidthProperty, value);
    }

    public string SearchQuery
    {
        get => (string)GetValue(SearchQueryProperty);
        set => SetValue(SearchQueryProperty, value);
    }

    public long SelectedItemId
    {
        get => (long)GetValue(SelectedItemIdProperty);
        set => SetValue(SelectedItemIdProperty, value);
    }

    public async void ShowPanel()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        await LoadCategoriesAsync(resetSelection: true);
        SearchBox.Text = string.Empty;
        SearchQuery = string.Empty;
        ResetSelection();
        await RefreshAsync();

        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 3;

        Show();
        Activate();
        SearchBox.Focus();
        UpdateQuickCardWidths();
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void QuickPanelWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async Task LoadCategoriesAsync(bool resetSelection = false)
    {
        var previous = CategoryFilterBox.SelectedItem as string;
        CategoryFilterBox.Items.Clear();
        CategoryFilterBox.Items.Add("全部分类");
        foreach (var tag in await _clipboardRepository.GetFavoriteTagsAsync())
        {
            CategoryFilterBox.Items.Add(tag);
        }

        CategoryFilterBox.SelectedItem = !resetSelection && !string.IsNullOrWhiteSpace(previous) && CategoryFilterBox.Items.Contains(previous)
            ? previous
            : "全部分类";
    }

    private string? SelectedCategory()
    {
        var selected = CategoryFilterBox.SelectedItem as string;
        return string.IsNullOrWhiteSpace(selected) || selected == "全部分类" ? null : selected;
    }

    private async Task RefreshAsync()
    {
        var results = await _clipboardRepository.SearchAsync(SearchBox.Text, 128);
        var selectedCategory = SelectedCategory();
        if (!string.IsNullOrWhiteSpace(selectedCategory))
        {
            results = results.Where(item => string.Equals(item.FavoriteTag, selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _favorites.Clear();
        _history.Clear();

        foreach (var item in results.Where(item => item.IsFavorite).Take(32))
        {
            _favorites.Add(item);
        }

        foreach (var item in results.Where(item => !item.IsFavorite).Take(64))
        {
            _history.Add(item);
        }

        NormalizeSelection(preferFirstItem: false);
    }

    private IReadOnlyList<ClipboardItem> CombinedItems()
        => _favorites.Concat(_history).ToList();

    private ClipboardItem? SelectedItem()
    {
        var items = CombinedItems();
        return items.FirstOrDefault(item => item.Id == SelectedItemId) ?? items.FirstOrDefault();
    }

    private void ResetSelection()
    {
        _selectedIndex = 0;
        SelectedItemId = 0;
    }

    private void NormalizeSelection(bool preferFirstItem)
    {
        var items = CombinedItems();
        if (items.Count == 0)
        {
            _selectedIndex = 0;
            SelectedItemId = 0;
            return;
        }

        var currentIndex = preferFirstItem ? -1 : items.ToList().FindIndex(item => item.Id == SelectedItemId);
        if (currentIndex < 0)
        {
            _selectedIndex = 0;
            SelectedItemId = items[0].Id;
            return;
        }

        _selectedIndex = currentIndex;
    }

    private void MoveSelection(int delta)
    {
        var items = CombinedItems();
        if (items.Count == 0)
        {
            SelectedItemId = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, items.Count - 1);
        SelectedItemId = items[_selectedIndex].Id;
    }

    private async Task PasteAsync(ClipboardItem? item)
    {
        if (item is null)
        {
            return;
        }

        Hide();
        await _pasteService.PasteAsync(item);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchQuery = SearchBox.Text;
        ResetSelection();
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private async void CategoryFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ResetSelection();
        await RefreshAsync();
    }

    private void QuickList_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateQuickCardWidths();

    private void UpdateQuickCardWidths()
    {
        UpdateCardWidth(FavoritesList, isFavoriteList: true);
        UpdateCardWidth(HistoryList, isFavoriteList: false);
    }

    private void UpdateCardWidth(FrameworkElement list, bool isFavoriteList)
    {
        var availableWidth = list.ActualWidth;
        if (availableWidth <= 0)
        {
            return;
        }

        const double gap = 12;
        const double minCardWidth = 210;
        const int maxColumns = 7;
        var bucketedWidth = Math.Floor(Math.Max(0, availableWidth - 4) / 8) * 8;
        var columns = Math.Clamp((int)Math.Floor((bucketedWidth + gap) / (minCardWidth + gap)), 1, maxColumns);
        var cardWidth = Math.Max(minCardWidth, Math.Floor((bucketedWidth - gap * columns) / columns));

        if (isFavoriteList)
        {
            if (columns == _favoriteColumns && Math.Abs(cardWidth - _lastFavoriteCardWidth) < 1)
            {
                return;
            }

            _favoriteColumns = columns;
            _lastFavoriteCardWidth = cardWidth;
            FavoriteCardWidth = cardWidth;
        }
        else
        {
            if (columns == _historyColumns && Math.Abs(cardWidth - _lastHistoryCardWidth) < 1)
            {
                return;
            }

            _historyColumns = columns;
            _lastHistoryCardWidth = cardWidth;
            HistoryCardWidth = cardWidth;
        }
    }

    private async void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PasteAsync(SelectedItem());
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            MoveVerticalSelection(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            MoveVerticalSelection(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            MoveSelection(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            MoveSelection(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private async void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsActionElement(e.OriginalSource as DependencyObject))
        {
            if (sender is Border card)
            {
                AnimateCard(card, 1, 1);
            }
            return;
        }

        if (sender is Border itemCard)
        {
            AnimateCard(itemCard, 1, 1);
        }
        var item = (sender as FrameworkElement)?.Tag as ClipboardItem;
        SelectItem(item);
        await PasteAsync(item);
    }

    private void ItemCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsActionElement(e.OriginalSource as DependencyObject) && sender is Border card)
        {
            SelectItem((sender as FrameworkElement)?.Tag as ClipboardItem);
            AnimateCard(card, 0.985, 1);
        }
    }

    private void SelectItem(ClipboardItem? item)
    {
        if (item is null)
        {
            return;
        }

        var items = CombinedItems();
        var index = items.ToList().FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0)
        {
            return;
        }

        _selectedIndex = index;
        SelectedItemId = item.Id;
    }

    private void MoveVerticalSelection(int direction)
    {
        var favoriteCount = _favorites.Count;
        var historyCount = _history.Count;
        if (favoriteCount + historyCount == 0)
        {
            SelectedItemId = 0;
            return;
        }

        if (_selectedIndex < favoriteCount)
        {
            MoveWithinSection(direction, sectionStart: 0, sectionCount: favoriteCount, columns: Math.Max(1, _favoriteColumns));
            return;
        }

        MoveWithinSection(direction, sectionStart: favoriteCount, sectionCount: historyCount, columns: Math.Max(1, _historyColumns));
    }

    private void MoveWithinSection(int direction, int sectionStart, int sectionCount, int columns)
    {
        if (sectionCount <= 0)
        {
            return;
        }

        var localIndex = Math.Clamp(_selectedIndex - sectionStart, 0, sectionCount - 1);
        var targetLocalIndex = localIndex + direction * columns;

        if (targetLocalIndex < 0)
        {
            if (sectionStart > 0)
            {
                var target = Math.Min(sectionStart - 1, localIndex);
                SetSelectionIndex(target);
            }
            else
            {
                SetSelectionIndex(sectionStart);
            }
            return;
        }

        if (targetLocalIndex >= sectionCount)
        {
            var nextSectionStart = sectionStart + sectionCount;
            var totalCount = _favorites.Count + _history.Count;
            if (nextSectionStart < totalCount)
            {
                var target = Math.Min(nextSectionStart + localIndex, totalCount - 1);
                SetSelectionIndex(target);
            }
            else
            {
                SetSelectionIndex(sectionStart + sectionCount - 1);
            }
            return;
        }

        SetSelectionIndex(sectionStart + targetLocalIndex);
    }

    private void SetSelectionIndex(int index)
    {
        var items = CombinedItems();
        if (items.Count == 0)
        {
            _selectedIndex = 0;
            SelectedItemId = 0;
            return;
        }

        _selectedIndex = Math.Clamp(index, 0, items.Count - 1);
        SelectedItemId = items[_selectedIndex].Id;
    }

    private async void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
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

        await LoadCategoriesAsync(resetSelection: false);
        await RefreshAsync();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
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

    private static void AnimateCard(Border card, double scale, double opacity)
    {
        if (card.RenderTransform is not ScaleTransform transform || transform.IsFrozen)
        {
            transform = new ScaleTransform(1, 1);
            card.RenderTransform = transform;
            card.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        }

        var duration = TimeSpan.FromMilliseconds(120);
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration));
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration));
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, duration));
    }
}
