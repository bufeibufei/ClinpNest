using System.Globalization;
using System.Windows.Data;

namespace ClipNest.Services;

public sealed class CardWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || width <= 0)
        {
            return 360d;
        }

        var columns = width switch
        {
            < 540 => 1,
            < 860 => 2,
            < 1180 => 3,
            _ => 4
        };

        const double gap = 18;
        return Math.Max(250, Math.Floor((width - gap * (columns - 1)) / columns));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
