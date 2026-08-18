using System.Net;
using Amazon.Runtime;
using R2Explorer.Models;

namespace R2Explorer.Services;

/// <summary>
/// 为 AWS SDK 提供带代理的 HttpClient，支持 HTTP / HTTPS / SOCKS5。
/// </summary>
public class ProxyHttpClientFactory : HttpClientFactory
{
    private readonly HttpClient _client;

    public ProxyHttpClientFactory(ProxySettings proxy)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = proxy.Enabled,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 32,
        };

        if (proxy.Enabled)
        {
            handler.Proxy = new WebProxy(proxy.ProxyUri) { Credentials = proxy.Credentials };
        }

        _client = new HttpClient(handler);
    }

    public override HttpClient GetHttpClient(IClientConfig clientConfig) => _client;

    public override void DisposeHttpClient(HttpClient httpClient)
    {
        // 由本工厂统一持有，不随单次请求释放
    }
}
