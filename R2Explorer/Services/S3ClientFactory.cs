using Amazon.S3;
using R2Explorer.Models;

namespace R2Explorer.Services;

/// <summary>
/// 根据帐号配置创建 AmazonS3Client（R2 与自定义 S3 端点统一走这里）。
/// </summary>
public static class S3ClientFactory
{
    public static AmazonS3Config BuildConfig(AccountProfile account, ProxySettings proxy)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = account.ServiceUrl,
            ForcePathStyle = account.Mode == AccountModes.S3Custom ? account.ForcePathStyle : true,
            Timeout = TimeSpan.FromSeconds(60),
            ReadWriteTimeout = TimeSpan.FromSeconds(60),
            HttpClientFactory = new ProxyHttpClientFactory(proxy),
        };

        if (account.Mode == AccountModes.S3Custom)
        {
            if (!string.IsNullOrWhiteSpace(account.Region))
            {
                config.AuthenticationRegion = account.Region.Trim();
            }
        }
        else
        {
            // R2 固定使用 "auto" 区域
            config.AuthenticationRegion = "auto";
        }

        return config;
    }

    public static async Task<AmazonS3Client> CreateAsync(
        AccountProfile account,
        ProxySettings proxy,
        CancellationToken ct = default)
    {
        var config = BuildConfig(account, proxy);

        switch (account.Mode)
        {
            case AccountModes.R2S3Token:
            case AccountModes.S3Custom:
                return new AmazonS3Client(account.AccessKeyId.Trim(), account.SecretAccessKey.Trim(), config);

            case AccountModes.R2ApiToken:
            case AccountModes.R2GlobalKey:
                var temp = await CloudflareApi.GetTempCredentialsAsync(account, proxy, ct);
                return new AmazonS3Client(temp.AccessKeyId, temp.SecretAccessKey, temp.SessionToken, config);

            default:
                throw new NotSupportedException($"未知的登录模式: {account.Mode}");
        }
    }
}
