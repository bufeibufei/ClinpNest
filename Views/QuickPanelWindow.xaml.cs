using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
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

    private readonly ClipboardRepository _clipboardRepository;
    private readonly PasteService _pasteService;
    private readonly ObservableCollection<ClipboardItem> _favorites = [];
    private readonly ObservableCollection<ClipboardItem> _history = [];
    private bool _forceClose;
    private int _favoriteColumns;
    private int _historyColumns;
    private double _lastFavoriteCardWidth;
    private double _lastHistoryCardWidth;

    public QuickPanelWindow(ClipboardRepository clipboardRepository, PasteService pasteService)
    {
        InitializeComponent();
        _clipboardRepository = clipboardRepository;
        _pasteService = pasteService;
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

    public async void ShowPanel()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        await LoadCategoriesAsync(resetSelection: true);
        SearchBox.Text = string.Empty;
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
    }

    private ClipboardItem? FirstItem()
        => _favorites.FirstOrDefault() ?? _history.FirstOrDefault();

    private async Task PasteAsync(ClipboardItem? item)
    {
        if (item is null)
        {
            return;
        }

        Hide();
        await _pasteService.PasteAsync(item);
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => await RefreshAsync();

    private async void CategoryFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshAsync();

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

    private async void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PasteAsync(FirstItem());
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
            return;
        }

        await PasteAsync((sender as FrameworkElement)?.Tag as ClipboardItem);
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
}
