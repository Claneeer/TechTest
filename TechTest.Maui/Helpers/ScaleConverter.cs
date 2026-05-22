using System.Globalization;

namespace TechTest.Maui.Helpers;

public class ScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double baseValue = 0;

        if (value is double d)
        {
            baseValue = d;
        }
        else if (value is int i)
        {
            baseValue = i;
        }
        else if (value is float f)
        {
            baseValue = f;
        }
        else if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
        {
            baseValue = parsed;
        }

        if (parameter is string paramStr && double.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double multiplier))
        {
            baseValue *= multiplier;
        }

        double scaled = baseValue * ResponsiveHelper.ScaleFactor;

        if (targetType == typeof(Thickness))
        {
            return new Thickness(scaled);
        }

        if (targetType == typeof(GridLength))
        {
            return new GridLength(scaled);
        }

        if (targetType == typeof(int))
        {
            return (int)Math.Round(scaled);
        }

        return scaled;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && ResponsiveHelper.ScaleFactor > 0)
        {
            return d / ResponsiveHelper.ScaleFactor;
        }

        return value;
    }
}
