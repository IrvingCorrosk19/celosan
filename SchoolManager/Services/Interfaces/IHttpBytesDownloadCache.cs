namespace SchoolManager.Services.Interfaces;

/// <summary>
/// Descarga bytes por HTTPS con caché en memoria (y disco opcional) para la misma URL.
/// </summary>
public interface IHttpBytesDownloadCache
{
    Task<byte[]?> GetOrDownloadAsync(string absoluteUrl, int maxBytes, TimeSpan timeout, CancellationToken cancellationToken = default);
}
