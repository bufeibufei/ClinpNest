using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ClipNest.Controls;

public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty RawTextProperty =
        DependencyProperty.Register(nameof(RawText), typeof(string), typeof(HighlightedTextBlock), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty QueryProperty =
        DependencyProperty.Register(nameof(Query), typeof(string), typeof(HighlightedTextBlock), new PropertyMetadata(string.Empty, OnTextChanged));

    public string RawText
    {
        get => (string)GetValue(RawTextProperty);
        set => SetValue(RawTextProperty, value);
    }

    public string Query
    {
        get => (string)GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((HighlightedTextBlock)dependencyObject).RefreshInlines();

    private void RefreshInlines()
    {
        Inlines.Clear();

        var text = RawText ?? string.Empty;
        var query = Query?.Trim();
        if (text.Length == 0 || string.IsNullOrWhiteSpace(query))
        {
            Inlines.Add(new Run(text));
            return;
        }

        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            Inlines.Add(new Run(text));
            return;
        }

        var cursor = 0;
        while (index >= 0)
        {
            if (index > cursor)
            {
                Inlines.Add(new Run(text[cursor..index]));
            }

            Inlines.Add(new Run(text.Substring(index, query.Length))
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 24, 40)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 237, 180))
            });

            cursor = index + query.Length;
            index = text.IndexOf(query, cursor, StringComparison.OrdinalIgnoreCase);
        }

        if (cursor < text.Length)
        {
            Inlines.Add(new Run(text[cursor..]));
        }
    }
}
