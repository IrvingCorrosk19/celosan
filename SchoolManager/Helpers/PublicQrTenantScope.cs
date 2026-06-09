using SchoolManager.Services.Implementations;

namespace SchoolManager.Helpers;

/// <summary>
/// Desactiva filtros multi-tenant durante consultas de endpoints públicos QR (sin sesión).
/// La autorización viene del token firmado HMAC, no del contexto de escuela.
/// </summary>
public sealed class PublicQrTenantScope : IDisposable
{
    private readonly TenantContext _tenant;
    private readonly bool _previous;

    public PublicQrTenantScope(TenantContext tenant)
    {
        _tenant = tenant;
        _previous = tenant.BypassTenantFilter;
        tenant.BypassTenantFilter = true;
    }

    public void Dispose() => _tenant.BypassTenantFilter = _previous;
}
