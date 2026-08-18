using System.Globalization;
using System.Windows.Data;

namespace R2Explorer.Converters;

/// <summary>R2Item.IsFolder -> 文件夹/文件图标（Segoe MDL2 Assets 字符）。</summary>
public class FolderIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "\uE8B7" : "\uE8A5";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
