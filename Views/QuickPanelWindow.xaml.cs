using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ClipNest.Data;
using ClipNest.Models;
using ClipNest.Services;

namespace ClipNest.Views;

public partial class QuickPanelWindow
{
    private readonly ClipboardRepository _clipboardRepository;
    private readonly PasteService _pasteService;
    private readonly ObservableCollection<ClipboardItem> _items = [];

    public QuickPanelWindow(ClipboardRepository clipboardRepository, PasteService pasteService)
    {
        InitializeComponent();
        _clipboardRepository = clipboardRepository;
        _pasteService = pasteService;
        ItemsList.ItemsSource = _items;
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
        if (_items.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
        }
    }

    private async Task RefreshAsync()
    {
        var results = await _clipboardRepository.SearchAsync(SearchBox.Text, 40);
        _items.Clear();
        foreach (var item in results)
        {
            _items.Add(item);
        }
    }

    private async Task PasteSelectedAsync()
    {
        if (ItemsList.SelectedItem is not ClipboardItem item)
        {
            return;
        }

        Hide();
        await _pasteService.PasteAsync(item);
    }

    private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => await RefreshAsync();

    private async void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down && _items.Count > 0)
        {
            ItemsList.Focus();
            ItemsList.SelectedIndex = Math.Min(ItemsList.SelectedIndex + 1, _items.Count - 1);
            e.Handled = true;
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

    private async void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => await PasteSelectedAsync();

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();
}
