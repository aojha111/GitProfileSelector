using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Media = System.Windows.Media;

namespace GitProfile.App;

/// <summary>Turns a #RRGGBB string from the model into a paint, so the colours can be tested in Core.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string hex
            ? new Media.SolidColorBrush((Media.Color)Media.ColorConverter.ConvertFromString(hex)!)
            : Media.Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
