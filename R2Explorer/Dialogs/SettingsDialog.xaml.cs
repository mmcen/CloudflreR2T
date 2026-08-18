using System.Windows;
using System.Windows.Controls;
using R2Explorer.Models;

namespace R2Explorer.Dialogs;

/// <summary>
/// 设置对话框：代理、托盘行为、并发数、主题等。
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly AppSettings _settings;

    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // 代理
        ChkProxyEnabled.IsChecked = settings.Proxy.Enabled;
        for (int i = 0; i < ProxyTypeCombo.Items.Count; i++)
        {
            if (ProxyTypeCombo.Items[i] is ComboBoxItem item && (string)item.Tag == settings.Proxy.Type)
            {
                ProxyTypeCombo.SelectedIndex = i;
                break;
            }
        }
        TxtProxyHost.Text = settings.Proxy.Host;
        TxtProxyPort.Text = settings.Proxy.Port.ToString();
        TxtProxyUser.Text = settings.Proxy.Username;
        TxtProxyPass.Password = settings.Proxy.Password;

        // 托盘
        ChkTrayOnClose.IsChecked = settings.MinimizeToTrayOnClose;
        ChkTrayOnMinimize.IsChecked = settings.MinimizeToTrayOnMinimize;
        ChkAutoConnect.IsChecked = settings.AutoConnectLast;

        // 传输 / 主题
        TxtConcurrency.Text = settings.MaxConcurrentTransfers.ToString();
        for (int i = 0; i < ThemeCombo.Items.Count; i++)
        {
            if (ThemeCombo.Items[i] is ComboBoxItem item && (string)item.Tag == settings.Theme)
            {
                ThemeCombo.SelectedIndex = i;
                break;
            }
        }

        ChkConfirmDelete.IsChecked = settings.ConfirmBeforeDelete;

        Proxy_Changed(null, null);
    }

    private void Proxy_Changed(object? sender, RoutedEventArgs? e)
        => ProxyFields.IsEnabled = ChkProxyEnabled.IsChecked == true;

    private async void BtnTestProxy_Click(object sender, RoutedEventArgs e)
    {
        var proxy = CollectProxy();
        if (proxy == null)
            return;

        BtnTestProxy.IsEnabled = false;
        ProxyTestResult.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        ProxyTestResult.Text = "正在测试代理...";

        try
        {
            using var handler = new System.Net.Http.HttpClientHandler
            {
                UseProxy = proxy.Enabled,
                Proxy = proxy.Enabled ? new System.Net.WebProxy(proxy.ProxyUri) { Credentials = proxy.Credentials } : null,
            };
            using var client = new System.Net.Http.HttpClient(handler) { Timeout = System.TimeSpan.FromSeconds(15) };
            var resp = await client.GetAsync("https://www.cloudflare.com/cdn-cgi/trace");
            if (resp.IsSuccessStatusCode)
            {
                ProxyTestResult.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                ProxyTestResult.Text = $"代理可用（HTTP {(int)resp.StatusCode}）。";
            }
            else
            {
                ProxyTestResult.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                ProxyTestResult.Text = $"代理可用，但目标返回 HTTP {(int)resp.StatusCode}。";
            }
        }
        catch (Exception ex)
        {
            ProxyTestResult.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            ProxyTestResult.Text = $"代理测试失败：{ex.Message}";
        }
        finally
        {
            BtnTestProxy.IsEnabled = true;
        }
    }

    private ProxySettings? CollectProxy()
    {
        if (!int.TryParse(TxtProxyPort.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show(this, "代理端口无效（1-65535）。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (ChkProxyEnabled.IsChecked == true && string.IsNullOrWhiteSpace(TxtProxyHost.Text))
        {
            MessageBox.Show(this, "启用代理时需要填写服务器地址。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var proxy = new ProxySettings
        {
            Enabled = ChkProxyEnabled.IsChecked == true,
            Type = ProxyTypeCombo.SelectedItem is ComboBoxItem item ? (string)item.Tag : "http",
            Host = TxtProxyHost.Text.Trim(),
            Port = port,
            Username = TxtProxyUser.Text,
            Password = TxtProxyPass.Password,
        };
        return proxy;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var proxy = CollectProxy();
        if (proxy == null)
            return;

        if (!int.TryParse(TxtConcurrency.Text.Trim(), out var concurrency) || concurrency < 1 || concurrency > 32)
        {
            MessageBox.Show(this, "并发数需在 1-32 之间。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.Proxy = proxy;
        _settings.MinimizeToTrayOnClose = ChkTrayOnClose.IsChecked == true;
        _settings.MinimizeToTrayOnMinimize = ChkTrayOnMinimize.IsChecked == true;
        _settings.AutoConnectLast = ChkAutoConnect.IsChecked == true;
        _settings.MaxConcurrentTransfers = concurrency;
        _settings.Theme = ThemeCombo.SelectedItem is ComboBoxItem item ? (string)item.Tag : "Dark";
        _settings.ConfirmBeforeDelete = ChkConfirmDelete.IsChecked == true;

        DialogResult = true;
    }
}
