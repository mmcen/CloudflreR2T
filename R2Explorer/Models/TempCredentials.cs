namespace R2Explorer.Models;

/// <summary>
/// R2 API Token / 全局 Key 模式通过 Cloudflare v4 API 换取的临时 S3 凭证。
/// </summary>
public class TempCredentials
{
    public string AccessKeyId { get; }
    public string SecretAccessKey { get; }
    public string SessionToken { get; }

    public TempCredentials(string accessKeyId, string secretAccessKey, string sessionToken)
    {
        AccessKeyId = accessKeyId;
        SecretAccessKey = secretAccessKey;
        SessionToken = sessionToken;
    }
}
