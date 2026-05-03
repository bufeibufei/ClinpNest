using System.Globalization;
using System.Windows.Data;

namespace ClipNest.Services;

public sealed class IdEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is null || values[1] is null)
        {
            return false;
        }

        return long.TryParse(values[0].ToString(), out var left) &&
               long.TryParse(values[1].ToString(), out var right) &&
               left == right;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
