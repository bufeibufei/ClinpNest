using System.Windows;
using System.Windows.Input;

namespace ClipNest.Views;

public partial class AddTagDialog
{
    public AddTagDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TagNameBox.Focus();
            TagNameBox.SelectAll();
        };
    }

    public string TagName => TagNameBox.Text.Trim();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        Save();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Save();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(TagName))
        {
            return;
        }

        DialogResult = true;
    }
}
