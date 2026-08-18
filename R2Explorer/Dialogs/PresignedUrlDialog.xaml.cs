using System.Windows;

namespace R2Explorer.Dialogs;

public partial class PresignedUrlDialog : Window
{
    private readonly Func<double, string> _generate;

    public PresignedUrlDialog(Func<double, string> generate, string objectName)
    {
        InitializeComponent();
        _generate = generate;
        Title = $"生成临时链接 - {objectName}";
        ExpiryCombo.SelectedIndex = 2; // 默认 1 小时
        Regenerate();
    }

    private double CurrentMinutes
        => ExpiryCombo.SelectedItem is ComboBoxItem item && double.TryParse(item.Tag as string, out var m) ? m : 60;

    private void Regenerate()
    {
        try
        {
            UrlBox.Text = _generate(CurrentMinutes);
        }
        catch (Exception ex)
        {
            UrlBox.Text = $"生成失败：{ex.Message}";
        }
    }

    private void ExpiryCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => Regenerate();

    private void BtnCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(UrlBox.Text) || UrlBox.Text.StartsWith("生成失败"))
        {
            MessageBox.Show(this, "当前没有可复制的链接。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Clipboard.SetText(UrlBox.Text);
        BtnCopyUrl.Content = "已复制";
    }
}
