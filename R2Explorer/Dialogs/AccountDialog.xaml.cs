using System.Windows;
using System.Windows.Controls;
using R2Explorer.Models;
using R2Explorer.Services;

namespace R2Explorer.Dialogs;

/// <summary>
/// 帐号新建 / 编辑对话框，支持四种登录模式与“测试连接”。
/// </summary>
public partial class AccountDialog : Window
{
    private readonly AccountProfile? _editing;
    private bool _suppressModeEvent;

    public AccountDialog(AccountProfile? editing = null)
    {
        InitializeComponent();
        _editing = editing;

        if (editing != null)
        {
            Title = $"编辑帐号 - {editing.Name}";
            TxtName.Text = editing.Name;
            TxtAccountId.Text = editing.AccountId;
            TxtAccessKey.Text = editing.AccessKeyId;
            TxtSecret.Password = editing.SecretAccessKey;
            TxtApiToken.Password = editing.ApiToken;
            TxtEmail.Text = editing.Email;
            TxtGlobalKey.Password = editing.GlobalApiKey;
            TxtEndpoint.Text = editing.EndpointUrl;
            TxtRegion.Text = editing.Region;
            ChkForcePathStyle.IsChecked = editing.ForcePathStyle;
            TxtPublicBase.Text = editing.PublicBaseUrl;

            _suppressModeEvent = true;
            for (int i = 0; i < ModeCombo.Items.Count; i++)
            {
                if (ModeCombo.Items[i] is ComboBoxItem item && (string)item.Tag == editing.Mode)
                {
                    ModeCombo.SelectedIndex = i;
                    break;
                }
            }
            _suppressModeEvent = false;
        }

        ApplyModeVisibility();
    }

    private void ApplyModeVisibility()
    {
        var mode = CurrentMode;

        RowAccountId.Visibility = mode == AccountModes.S3Custom ? Visibility.Collapsed : Visibility.Visible;
        RowEndpoint.Visibility = mode == AccountModes.S3Custom ? Visibility.Visible : Visibility.Collapsed;

        var showAccess = mode is AccountModes.R2S3Token or AccountModes.S3Custom;
        RowAccess.Visibility = showAccess ? Visibility.Visible : Visibility.Collapsed;
        RowApiToken.Visibility = mode == AccountModes.R2ApiToken ? Visibility.Visible : Visibility.Collapsed;
        RowGlobal.Visibility = mode == AccountModes.R2GlobalKey ? Visibility.Visible : Visibility.Collapsed;
        RowRegion.Visibility = mode == AccountModes.S3Custom ? Visibility.Visible : Visibility.Collapsed;
        ChkForcePathStyle.Visibility = mode == AccountModes.S3Custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private string CurrentMode =>
        ModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag ? tag : AccountModes.R2S3Token;

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressModeEvent)
            return;
        ApplyModeVisibility();
    }

    private AccountProfile? CollectProfile()
    {
        var name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show(this, "请输入帐号名称。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var profile = _editing ?? new AccountProfile();
        profile.Name = name;
        profile.Mode = CurrentMode;

        switch (CurrentMode)
        {
            case AccountModes.R2S3Token:
                if (string.IsNullOrWhiteSpace(TxtAccountId.Text) ||
                    string.IsNullOrWhiteSpace(TxtAccessKey.Text) ||
                    string.IsNullOrEmpty(TxtSecret.Password))
                {
                    MessageBox.Show(this, "R2 S3 API Token 模式需要填写 Account ID、Access Key ID 与 Secret Access Key。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                profile.AccountId = TxtAccountId.Text.Trim();
                profile.AccessKeyId = TxtAccessKey.Text.Trim();
                profile.SecretAccessKey = TxtSecret.Password;
                break;

            case AccountModes.R2ApiToken:
                if (string.IsNullOrWhiteSpace(TxtAccountId.Text) || string.IsNullOrEmpty(TxtApiToken.Password))
                {
                    MessageBox.Show(this, "R2 API Token 模式需要填写 Account ID 与 API Token。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                profile.AccountId = TxtAccountId.Text.Trim();
                profile.ApiToken = TxtApiToken.Password;
                break;

            case AccountModes.R2GlobalKey:
                if (string.IsNullOrWhiteSpace(TxtAccountId.Text) ||
                    string.IsNullOrWhiteSpace(TxtEmail.Text) ||
                    string.IsNullOrEmpty(TxtGlobalKey.Password))
                {
                    MessageBox.Show(this, "全局 API Key 模式需要填写 Account ID、邮箱与 Global API Key。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                profile.AccountId = TxtAccountId.Text.Trim();
                profile.Email = TxtEmail.Text.Trim();
                profile.GlobalApiKey = TxtGlobalKey.Password;
                break;

            case AccountModes.S3Custom:
                if (string.IsNullOrWhiteSpace(TxtEndpoint.Text) ||
                    string.IsNullOrWhiteSpace(TxtAccessKey.Text) ||
                    string.IsNullOrEmpty(TxtSecret.Password))
                {
                    MessageBox.Show(this, "自定义 S3 端点模式需要填写端点地址、Access Key ID 与 Secret Access Key。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                profile.EndpointUrl = TxtEndpoint.Text.Trim();
                profile.AccessKeyId = TxtAccessKey.Text.Trim();
                profile.SecretAccessKey = TxtSecret.Password;
                profile.Region = TxtRegion.Text.Trim();
                profile.ForcePathStyle = ChkForcePathStyle.IsChecked == true;
                break;
        }

        profile.PublicBaseUrl = TxtPublicBase.Text.Trim();
        return profile;
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var profile = CollectProfile();
        if (profile == null)
            return;

        BtnTest.IsEnabled = false;
        TestResult.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        TestResult.Text = "正在测试连接...";

        try
        {
            var client = await S3ClientFactory.CreateAsync(profile, App.Settings.Proxy);
            using (client)
            {
                var buckets = await client.ListBucketsAsync();
                TestResult.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                TestResult.Text = $"连接成功！共 {buckets.Buckets.Count} 个存储桶。";
            }
        }
        catch (Exception ex)
        {
            TestResult.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            TestResult.Text = $"连接失败：{ex.Message}";
        }
        finally
        {
            BtnTest.IsEnabled = true;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var profile = CollectProfile();
        if (profile == null)
            return;
        _editingProfile = profile;
        DialogResult = true;
    }

    private AccountProfile? _editingProfile;
    public AccountProfile? ResultProfile => _editingProfile;
}
