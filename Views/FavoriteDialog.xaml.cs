using ClipNest.Models;
using System.Windows;

namespace ClipNest.Views;

public partial class FavoriteDialog
{
    public FavoriteDialog(ClipboardItem item, IEnumerable<string> tags)
    {
        InitializeComponent();
        AliasBox.Text = string.IsNullOrWhiteSpace(item.FavoriteAlias) ? SuggestedAlias(item.ContentText) : item.FavoriteAlias;
        foreach (var tag in tags)
        {
            TagBox.Items.Add(tag);
        }

        TagBox.Text = item.FavoriteTag;
        PreviewText.Text = item.Preview;
        PreviewText.ToolTip = item.ContentText;
        PreviewTimeText.Text = item.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        PreviewSourceText.Text = item.SourceApp;
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

    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddTagDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var tag = dialog.TagName;
        if (string.IsNullOrWhiteSpace(tag) || TagBox.Items.Contains(tag))
        {
            return;
        }

        TagBox.Items.Add(tag);
        TagBox.SelectedItem = tag;
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
