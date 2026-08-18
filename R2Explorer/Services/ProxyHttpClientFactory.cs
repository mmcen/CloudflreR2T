using System.Net;
using System.Net.Http;
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

    public override HttpClient CreateHttpClient(IClientConfig clientConfig) => _client;

    /// <summary>工厂自身持有一个长期复用的 HttpClient，无需 SDK 缓存或释放。</summary>
    public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

    public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
}
