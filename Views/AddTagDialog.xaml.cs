using System.Windows;

namespace ClipNest.Views;

public partial class AddTagDialog
{
    public AddTagDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TagNameBox.Focus();
    }

    public string TagName => TagNameBox.Text.Trim();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TagName))
        {
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
