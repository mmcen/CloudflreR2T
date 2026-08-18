using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using R2Explorer.Models;

namespace R2Explorer.Services;

/// <summary>
/// Cloudflare v4 API 调用。
/// 用于“R2 API Token / 全局 API Key”模式换取临时 S3 凭证。
/// </summary>
public static class CloudflareApi
{
    /// <summary>
    /// 调用 R2 临时凭证接口：
    /// POST https://api.cloudflare.com/client/v4/accounts/{account_id}/r2/temp-access-credentials
    /// </summary>
    public static async Task<TempCredentials> GetTempCredentialsAsync(
        AccountProfile account,
        ProxySettings proxy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(account.AccountId))
            throw new InvalidOperationException("缺少 Account ID。");

        var url = $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString(account.AccountId.Trim())}/r2/temp-access-credentials";

        using var handler = new HttpClientHandler { UseProxy = proxy.Enabled };
        if (proxy.Enabled)
        {
            handler.Proxy = new WebProxy(proxy.ProxyUri) { Credentials = proxy.Credentials };
        }

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        if (account.Mode == AccountModes.R2ApiToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.ApiToken.Trim());
        }
        else
        {
            request.Headers.Add("X-Auth-Email", account.Email.Trim());
            request.Headers.Add("X-Auth-Key", account.GlobalApiKey.Trim());
        }

        const string body = "{\"permission\":{\"effect\":\"allow\",\"action\":[\"*\"]}}";
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"获取 R2 临时凭证失败 (HTTP {(int)response.StatusCode}): {Truncate(text, 400)}");

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
            throw new Exception($"接口未返回凭证: {Truncate(text, 400)}");

        var accessKey = result.GetProperty("access_key_id").GetString() ?? throw new Exception("返回中缺少 access_key_id");
        var secretKey = result.GetProperty("secret_access_key").GetString() ?? throw new Exception("返回中缺少 secret_access_key");
        var sessionToken = result.GetProperty("session_token").GetString() ?? "";

        return new TempCredentials(accessKey, secretKey, sessionToken);
    }

    private static string Truncate(string s, int n)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= n ? s : s[..n]);
}
