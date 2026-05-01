using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipNest.Data;
using ClipNest.Models;
using ClipNest.Services;

namespace ClipNest.Views;

public partial class QuickPanelWindow
{
    private readonly ClipboardRepository _clipboardRepository;
    private readonly PasteService _pasteService;
    private readonly ObservableCollection<ClipboardItem> _favorites = [];
    private readonly ObservableCollection<ClipboardItem> _history = [];

    public QuickPanelWindow(ClipboardRepository clipboardRepository, PasteService pasteService)
    {
        InitializeComponent();
        _clipboardRepository = clipboardRepository;
        _pasteService = pasteService;
        FavoritesList.ItemsSource = _favorites;
        HistoryList.ItemsSource = _history;
    }

    public async void ShowPanel()
    {
        SearchBox.Text = string.Empty;
        await RefreshAsync();

        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 3;

        Show();
        Activate();
        SearchBox.Focus();
        SelectFirstAvailable();
    }

    private async Task RefreshAsync()
    {
        var results = await _clipboardRepository.SearchAsync(SearchBox.Text, 80);
        _favorites.Clear();
        _history.Clear();

        foreach (var item in results.Where(item => item.IsFavorite).Take(20))
        {
            _favorites.Add(item);
        }

        foreach (var item in results.Where(item => !item.IsFavorite).Take(40))
        {
            _history.Add(item);
        }

        SelectFirstAvailable();
    }

    private void SelectFirstAvailable()
    {
        if (_favorites.Count > 0)
        {
            FavoritesList.SelectedIndex = 0;
            HistoryList.SelectedIndex = -1;
        }
        else if (_history.Count > 0)
        {
            HistoryList.SelectedIndex = 0;
            FavoritesList.SelectedIndex = -1;
        }
    }

    private ClipboardItem? SelectedItem()
        => FavoritesList.SelectedItem as ClipboardItem ?? HistoryList.SelectedItem as ClipboardItem;

    private async Task PasteSelectedAsync()
    {
        var item = SelectedItem();
        if (item is null)
        {
            return;
        }

        Hide();
        await _pasteService.PasteAsync(item);
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => await RefreshAsync();

    private async void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var target = _favorites.Count > 0 ? FavoritesList : HistoryList;
            if (target.Items.Count > 0)
            {
                target.Focus();
                target.SelectedIndex = Math.Max(0, target.SelectedIndex);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            await PasteSelectedAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private async void ItemsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox list)
        {
            if (list == FavoritesList)
            {
                HistoryList.SelectedIndex = -1;
            }
            else
            {
                FavoritesList.SelectedIndex = -1;
            }
        }

        if (e.Key == Key.Enter)
        {
            await PasteSelectedAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private async void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox list)
        {
            if (list == FavoritesList)
            {
                HistoryList.SelectedIndex = -1;
            }
            else
            {
                FavoritesList.SelectedIndex = -1;
            }
        }

        await PasteSelectedAsync();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();
}
