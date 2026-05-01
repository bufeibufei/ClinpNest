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
    }

    private async Task RefreshAsync()
    {
        var results = await _clipboardRepository.SearchAsync(SearchBox.Text, 96);
        _favorites.Clear();
        _history.Clear();

        foreach (var item in results.Where(item => item.IsFavorite).Take(24))
        {
            _favorites.Add(item);
        }

        foreach (var item in results.Where(item => !item.IsFavorite).Take(48))
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
        await PasteAsync((sender as FrameworkElement)?.Tag as ClipboardItem);
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
