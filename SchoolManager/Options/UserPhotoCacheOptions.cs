namespace SchoolManager.Options;

/// <summary>
/// Caché de descargas HTTP (p. ej. Cloudinary) para PDFs y lectura de fotos por URL remota.
/// No sustituye Cloudinary; solo evita repetir la misma descarga en memoria/disco temporal.
/// </summary>
public class UserPhotoCacheOptions
{
    public const string SectionName = "UserPhotoCache";

    public bool Enabled { get; set; } = true;

    public int MemoryEntryTtlSeconds { get; set; } = 600;

    public int MemoryMaxEntryBytes { get; set; } = 5_242_880;

    public long MemoryCacheSizeLimitBytes { get; set; } = 134_217_728;

    public bool DiskCacheEnabled { get; set; } = true;

    public string DiskRelativePath { get; set; } = "cache/http-images";

    public long DiskMaxFileBytes { get; set; } = 4_194_304;
}
