using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using R2Explorer.Models;

namespace R2Explorer.Converters;

/// <summary>TransferState -> 颜色画刷（等待灰 / 进行中橙 / 完成绿 / 失败红 / 取消灰）。</summary>
public class TransferStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var brush = value switch
        {
            TransferState.Running => "AccentBrush",
            TransferState.Completed => "SuccessBrush",
            TransferState.Failed => "DangerBrush",
            _ => "TextSecondaryBrush",
        };

        try
        {
            return (Brush)System.Windows.Application.Current!.Resources[brush];
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
