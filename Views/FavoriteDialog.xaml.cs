using ClipNest.Models;
using System.Windows;

namespace ClipNest.Views;

public partial class FavoriteDialog
{
    public FavoriteDialog(ClipboardItem item)
    {
        InitializeComponent();
        AliasBox.Text = string.IsNullOrWhiteSpace(item.FavoriteAlias) ? SuggestedAlias(item.ContentText) : item.FavoriteAlias;
        TagBox.Text = item.FavoriteTag;
        PreviewText.Text = item.Preview;
        RemoveButton.Visibility = item.IsFavorite ? Visibility.Visible : Visibility.Collapsed;
    }

    public string Alias => AliasBox.Text.Trim();
    public string FavoriteTag => TagBox.Text.Trim();
    public bool RemoveFavorite { get; private set; }

    private static string SuggestedAlias(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 28 ? normalized : normalized[..28];
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveFavorite = true;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
