using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SchoolManager.Options;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

/// <summary>
/// Caché de descargas HTTP: memoria + disco bajo ContentRoot/cache.
/// </summary>
public sealed class HttpBytesDownloadCache : IHttpBytesDownloadCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<UserPhotoCacheOptions> _options;
    private readonly ILogger<HttpBytesDownloadCache> _logger;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public HttpBytesDownloadCache(
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        IOptions<UserPhotoCacheOptions> options,
        ILogger<HttpBytesDownloadCache> logger)
    {
        _memoryCache       = memoryCache;
        _httpClientFactory = httpClientFactory;
        _env               = env;
        _options           = options;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetOrDownloadAsync(string absoluteUrl, int maxBytes, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(absoluteUrl)
            || (!absoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !absoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            return null;

        var opt = _options.Value;
        if (!opt.Enabled)
            return await DownloadOnceAsync(absoluteUrl, maxBytes, timeout, cancellationToken).ConfigureAwait(false);

        var cacheKey = "httpbytes:v1:" + absoluteUrl;

        if (_memoryCache.TryGetValue(cacheKey, out byte[]? cached) && cached is { Length: > 0 })
            return cached;

        var gate = Gates.GetOrAdd(absoluteUrl, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cached) && cached is { Length: > 0 })
                return cached;

            var diskPath = opt.DiskCacheEnabled ? GetDiskPath(absoluteUrl, opt) : null;
            if (diskPath != null && File.Exists(diskPath))
            {
                var fromDisk = await File.ReadAllBytesAsync(diskPath, cancellationToken).ConfigureAwait(false);
                if (fromDisk.Length > 0 && fromDisk.Length <= maxBytes)
                {
                    PutMemory(cacheKey, fromDisk, opt);
                    return fromDisk;
                }
            }

            var bytes = await DownloadOnceAsync(absoluteUrl, maxBytes, timeout, cancellationToken).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
                return null;

            if (opt.DiskCacheEnabled && diskPath != null && bytes.Length <= opt.DiskMaxFileBytes)
            {
                try
                {
                    var dir = Path.GetDirectoryName(diskPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(diskPath, bytes, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HttpBytesCache] No se pudo escribir disco para {Url}", absoluteUrl);
                }
            }

            PutMemory(cacheKey, bytes, opt);
            return bytes;
        }
        finally
        {
            gate.Release();
        }
    }

    private void PutMemory(string cacheKey, byte[] bytes, UserPhotoCacheOptions opt)
    {
        if (bytes.Length > opt.MemoryMaxEntryBytes)
            return;

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Clamp(opt.MemoryEntryTtlSeconds, 60, 86_400)),
            Size = bytes.Length
        };
        _memoryCache.Set(cacheKey, bytes, entryOptions);
    }

    private string? GetDiskPath(string absoluteUrl, UserPhotoCacheOptions opt)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(absoluteUrl))).ToLowerInvariant();
        var root = Path.Combine(_env.ContentRootPath, opt.DiskRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        return Path.Combine(root, hash[..2], hash + ".bin");
    }

    private async Task<byte[]?> DownloadOnceAsync(string absoluteUrl, int maxBytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            var client = _httpClientFactory.CreateClient();
            client.Timeout = timeout;
            var bytes = await client.GetByteArrayAsync(new Uri(absoluteUrl), cts.Token).ConfigureAwait(false);
            if (bytes.Length > maxBytes)
            {
                _logger.LogWarning("[HttpBytesCache] Respuesta demasiado grande ({Len} > {Max}) {Url}", bytes.Length, maxBytes, absoluteUrl);
                return null;
            }

            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HttpBytesCache] Error descargando {Url}", absoluteUrl);
            return null;
        }
    }
}
