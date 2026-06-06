using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class TenantContext : ITenantContext
{
    public Guid? SchoolId { get; set; }
    public bool BypassTenantFilter { get; set; }
}

/// <summary>Tenant desactivado (migraciones CLI, tests sin HTTP).</summary>
public sealed class BypassTenantContext : ITenantContext
{
    public static readonly BypassTenantContext Instance = new();

    public Guid? SchoolId => null;
    public bool BypassTenantFilter => true;

    private BypassTenantContext() { }
}
