using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using R2Explorer.Models;

namespace R2Explorer.Services;

/// <summary>
/// R2 / S3 操作封装：桶与对象的增删改查、上传下载、预签名 URL 等。
/// 对“临时凭证”模式，在凭证过期前会自动重新获取。
/// </summary>
public class R2Service : IAsyncDisposable
{
    public AmazonS3Client Client { get; private set; }
    public AccountProfile Account { get; }

    private readonly ProxySettings _proxy;
    private TempCredentials? _temp;
    private DateTime _tempExpiresUtc = DateTime.MinValue;

    private R2Service(AmazonS3Client client, AccountProfile account, ProxySettings proxy)
    {
        Client = client;
        Account = account;
        _proxy = proxy;
    }

    public static async Task<R2Service> ConnectAsync(
        AccountProfile account,
        ProxySettings proxy,
        CancellationToken ct = default)
    {
        var client = await S3ClientFactory.CreateAsync(account, proxy, ct);
        return new R2Service(client, account, proxy);
    }

    /// <summary>连接后立即验证凭据是否可用。</summary>
    public async Task ValidateAsync(CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        await Client.ListBucketsAsync(ct);
    }

    private async Task EnsureClientValidAsync(CancellationToken ct)
    {
        if (!Account.UsesTempCredentials)
            return;

        var needsRefresh = _temp == null || DateTime.UtcNow.AddMinutes(5) >= _tempExpiresUtc;
        if (!needsRefresh)
            return;

        var temp = await CloudflareApi.GetTempCredentialsAsync(Account, _proxy, ct);
        var oldClient = Client;
        var config = S3ClientFactory.BuildConfig(Account, _proxy);
        Client = new AmazonS3Client(temp.AccessKeyId, temp.SecretAccessKey, temp.SessionToken, config);
        _temp = temp;
        _tempExpiresUtc = DateTime.UtcNow.AddSeconds(3600);
        oldClient.Dispose();
    }

    // ---------------- 桶 ----------------

    public async Task<List<string>> ListBucketsAsync(CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        var resp = await Client.ListBucketsAsync(ct);
        return resp.Buckets
            .Select(b => b.BucketName)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task CreateBucketAsync(string name, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        var request = new PutBucketRequest
        {
            BucketName = name,
            BucketRegionName = Account.Mode == AccountModes.S3Custom ? null : "auto",
            UseClientRegion = false,
        };
        await Client.PutBucketAsync(request, ct);
    }

    public async Task DeleteBucketAsync(string name, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        await Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = name }, ct);
    }

    // ---------------- 对象 ----------------

    /// <summary>列出指定前缀下的“一屏”内容（文件夹 + 文件），文件夹以 '/' 结尾的 key 表示。</summary>
    public async Task<List<R2Item>> ListObjectsAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        var items = new List<R2Item>();
        var request = new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = prefix,
            Delimiter = "/",
        };

        ListObjectsV2Response resp;
        do
        {
            resp = await Client.ListObjectsV2Async(request, ct);

            foreach (var cp in resp.CommonPrefixes)
            {
                var display = cp;
                if (display.StartsWith(prefix, StringComparison.Ordinal))
                    display = display[prefix.Length..];
                display = display.TrimEnd('/');

                items.Add(new R2Item
                {
                    Name = display,
                    Key = cp,
                    IsFolder = true,
                });
            }

            foreach (var obj in resp.S3Objects)
            {
                var display = obj.Key.StartsWith(prefix, StringComparison.Ordinal)
                    ? obj.Key[prefix.Length..]
                    : obj.Key;

                items.Add(new R2Item
                {
                    Name = display,
                    Key = obj.Key,
                    IsFolder = false,
                    Size = obj.Size,
                    LastModified = obj.LastModified,
                    ETag = obj.ETag,
                });
            }

            request.ContinuationToken = resp.NextContinuationToken;
        } while (resp.IsTruncated);

        // 文件夹在前，按名称排序
        return items
            .OrderBy(i => i.IsFolder ? 0 : 1)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>递归列出前缀下的所有对象 key（含文件夹标记对象）。</summary>
    public async Task<List<string>> ListAllKeysAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        var keys = new List<string>();
        var request = new ListObjectsV2Request { BucketName = bucket, Prefix = prefix };

        ListObjectsV2Response resp;
        do
        {
            resp = await Client.ListObjectsV2Async(request, ct);
            keys.AddRange(resp.S3Objects.Select(o => o.Key));
            request.ContinuationToken = resp.NextContinuationToken;
        } while (resp.IsTruncated);

        return keys;
    }

    /// <summary>创建“文件夹”（上传一个以 '/' 结尾的空对象）。</summary>
    public async Task CreateFolderAsync(string bucket, string folderKey, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        await Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = folderKey,
            ContentBody = "",
        }, ct);
    }

    public async Task UploadFileAsync(
        string bucket,
        string key,
        string filePath,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        await EnsureClientValidAsync(ct);
        using var transfer = new TransferUtility(Client);
        var request = new TransferUtilityUploadRequest
        {
            BucketName = bucket,
            Key = key,
            FilePath = filePath,
            PartSize = 16 * 1024 * 1024,
            AutoCloseStream = true,
        };
        if (progress != null)
        {
            request.UploadProgressEvent += (_, e) =>
                progress.Report(new TransferProgress(e.TransferredBytes, e.TotalBytes));
        }
        await transfer.UploadAsync(request, ct);
    }

    public async Task DownloadFileAsync(
        string bucket,
        string key,
        string localPath,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        await EnsureClientValidAsync(ct);
        using var transfer = new TransferUtility(Client);
        var request = new TransferUtilityDownloadRequest
        {
            BucketName = bucket,
            Key = key,
            FilePath = localPath,
        };
        if (progress != null)
        {
            request.WriteObjectProgressEvent += (_, e) =>
                progress.Report(new TransferProgress(e.TransferredBytes, e.TotalBytes));
        }
        await transfer.DownloadAsync(request, ct);
    }

    public async Task DeleteObjectsAsync(string bucket, IEnumerable<string> keys, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        var all = keys.Distinct().ToList();
        for (int i = 0; i < all.Count; i += 1000)
        {
            var chunk = all.Skip(i).Take(1000)
                .Select(k => new KeyVersion { Key = k })
                .ToList();
            await Client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = bucket,
                Objects = chunk,
                Quiet = true,
            }, ct);
        }
    }

    public async Task CopyObjectAsync(string bucket, string sourceKey, string destinationKey, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        await Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = bucket,
            SourceKey = sourceKey,
            DestinationBucket = bucket,
            DestinationKey = destinationKey,
        }, ct);
    }

    public async Task<GetObjectMetadataResponse> GetMetadataAsync(string bucket, string key, CancellationToken ct = default)
    {
        await EnsureClientValidAsync(ct);
        return await Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = bucket,
            Key = key,
        }, ct);
    }

    // ---------------- URL ----------------

    public string GetPresignedUrl(string bucket, string key, double expiresMinutes)
        => Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiresMinutes),
        });

    /// <summary>基于公开访问域名构造对象 URL；未配置时返回空字符串。</summary>
    public string BuildPublicUrl(string bucket, string key)
    {
        var baseUrl = Account.PublicBaseUrl?.Trim().TrimEnd('/') ?? "";
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";
        return $"{baseUrl}/{bucket}/{key}";
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
