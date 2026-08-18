using System.Net;
using System.Text.Json.Serialization;

namespace R2Explorer.Models;

/// <summary>
/// 全局应用设置，以 JSON 形式持久化到 %APPDATA%\R2Explorer\settings.json。
/// </summary>
public class AppSettings
{
    public List<AccountProfile> Accounts { get; set; } = new();
    public string? LastAccountId { get; set; }
    public string LastBucket { get; set; } = "";

    public ProxySettings Proxy { get; set; } = new();

    // 窗口与托盘
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool MinimizeToTrayOnMinimize { get; set; }
    public bool AutoConnectLast { get; set; } = true;

    // 传输
    public int MaxConcurrentTransfers { get; set; } = 4;

    // 界面
    public string Theme { get; set; } = "Dark";
    public bool ConfirmBeforeDelete { get; set; } = true;
}

/// <summary>
/// 代理设置，支持 HTTP / HTTPS / SOCKS5，应用于所有 S3 与 Cloudflare API 请求。
/// </summary>
public class ProxySettings
{
    public bool Enabled { get; set; }
    public string Type { get; set; } = "http";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7890;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    [JsonIgnore]
    public string ProxyUri => $"{Type}://{Host.Trim()}:{Port}";

    [JsonIgnore]
    public NetworkCredential? Credentials =>
        string.IsNullOrWhiteSpace(Username) ? null : new NetworkCredential(Username, Password);

    public string Display => Enabled ? $"{Type}://{Host}:{Port}" : "未启用";
}
