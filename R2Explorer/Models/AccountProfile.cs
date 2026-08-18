using System.Text.Json.Serialization;

namespace R2Explorer.Models;

/// <summary>
/// 帐号登录模式。
/// </summary>
public static class AccountModes
{
    /// <summary>R2 S3 API Token（Account ID + Access Key ID + Secret Access Key），直接使用 S3 签名访问。</summary>
    public const string R2S3Token = "r2-token";

    /// <summary>R2 API Token，通过 Cloudflare v4 API 换取临时 S3 凭证。</summary>
    public const string R2ApiToken = "r2-api-token";

    /// <summary>Cloudflare 全局 API Key（邮箱 + 全局 Key），同样换取临时凭证。</summary>
    public const string R2GlobalKey = "r2-global-key";

    /// <summary>任意 S3 兼容端点（如 MinIO、AWS S3、阿里云 OSS 等）。</summary>
    public const string S3Custom = "s3-custom";

    public static string Display(string mode) => mode switch
    {
        R2S3Token => "R2 S3 API Token（推荐）",
        R2ApiToken => "R2 API Token（临时凭证）",
        R2GlobalKey => "Cloudflare 全局 API Key",
        S3Custom => "自定义 S3 端点",
        _ => mode,
    };
}

/// <summary>
/// 一个 R2 / S3 帐号配置。
/// </summary>
public class AccountProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新帐号";
    public string Mode { get; set; } = AccountModes.R2S3Token;

    // R2 各模式的公共字段
    public string AccountId { get; set; } = "";

    // r2-token / s3-custom 模式
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";

    // r2-api-token 模式
    public string ApiToken { get; set; } = "";

    // r2-global-key 模式
    public string Email { get; set; } = "";
    public string GlobalApiKey { get; set; } = "";

    // s3-custom 模式
    public string EndpointUrl { get; set; } = "";
    public string Region { get; set; } = "";
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>可选：公开访问域名（如 https://pub-xxxx.r2.dev），用于“复制 URL”。</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>S3 服务端点地址。</summary>
    [JsonIgnore]
    public string ServiceUrl =>
        Mode == AccountModes.S3Custom
            ? EndpointUrl.Trim().TrimEnd('/')
            : $"https://{AccountId.Trim()}.r2.cloudflarestorage.com";

    /// <summary>是否为需要获取临时凭证的模式。</summary>
    [JsonIgnore]
    public bool UsesTempCredentials => Mode == AccountModes.R2ApiToken || Mode == AccountModes.R2GlobalKey;
}
