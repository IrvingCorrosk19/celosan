namespace SchoolManager.Services.Interfaces;

/// <summary>Contexto de tenant por request (escuela actual o bypass SuperAdmin).</summary>
public interface ITenantContext
{
    Guid? SchoolId { get; }

    /// <summary>Si true, no se aplican filtros globales por escuela (SuperAdmin, CLI, diseño).</summary>
    bool BypassTenantFilter { get; }
}
