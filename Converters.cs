using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GeminiWidget;

/// <summary>
/// Devuelve Visible cuando el valor es <c>false</c> y Collapsed cuando es <c>true</c>.
/// Se usa para mostrar el botón de inicio de sesión solo cuando NO hay sesión.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
