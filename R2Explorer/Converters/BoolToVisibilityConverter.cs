using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace R2Explorer.Converters;

/// <summary>bool -> Visibility；Invert 置反。true 显示 / false 折叠。</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        if (Invert)
            b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
