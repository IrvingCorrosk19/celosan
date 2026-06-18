using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class PrematriculationPeriodService : IPrematriculationPeriodService
{
    private readonly SchoolDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PrematriculationPeriodService> _logger;

    public PrematriculationPeriodService(
        SchoolDbContext context,
        ITenantContext tenantContext,
        ILogger<PrematriculationPeriodService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PrematriculationPeriod?> GetActivePeriodAsync(Guid schoolId)
    {
        var now = DateTime.UtcNow;
        return await _context.PrematriculationPeriods
            .Where(p => p.SchoolId == schoolId 
                && p.IsActive 
                && p.StartDate <= now 
                && p.EndDate >= now)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<PrematriculationPeriod?> GetByIdAsync(Guid id)
    {
        var period = await _context.PrematriculationPeriods
            .Include(p => p.School)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null)
            return null;

        if (!SchoolTenantHelper.CanAccessResource(period.SchoolId, _tenantContext))
            return null;

        return period;
    }

    public async Task<List<PrematriculationPeriodDto>> GetAllAsync(Guid schoolId)
    {
        var now = DateTime.UtcNow;
        return await _context.PrematriculationPeriods
            .Where(p => p.SchoolId == schoolId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PrematriculationPeriodDto
            {
                Id = p.Id,
                SchoolId = p.SchoolId,
                Name = p.Name,
                AcademicYearId = p.AcademicYearId,
                TrimesterId = p.TrimesterId,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                IsActive = p.IsActive,
                MaxCapacityPerGroup = p.MaxCapacityPerGroup,
                MaxSubjectsAllowed = p.MaxSubjectsAllowed,
                AutoAssignByShift = p.AutoAssignByShift,
                CreatedAt = p.CreatedAt,
                IsPeriodActive = p.StartDate <= now && p.EndDate >= now
            })
            .ToListAsync();
    }

    public async Task<PrematriculationPeriod> CreateAsync(PrematriculationPeriod period, Guid createdBy)
    {
        period.CreatedBy = createdBy;
        period.CreatedAt = DateTime.UtcNow;
        
        // Si este es activo, desactivar los demás períodos de la escuela
        if (period.IsActive)
        {
            var existingActive = await _context.PrematriculationPeriods
                .Where(p => p.SchoolId == period.SchoolId && p.IsActive)
                .ToListAsync();
            
            foreach (var existing in existingActive)
            {
                existing.IsActive = false;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = createdBy;
            }
        }

        _context.PrematriculationPeriods.Add(period);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Período de prematrícula creado: {PeriodId} para escuela {SchoolId}", period.Id, period.SchoolId);
        
        return period;
    }

    public async Task<PrematriculationPeriod> UpdateAsync(PrematriculationPeriod period, Guid updatedBy)
    {
        var existing = await _context.PrematriculationPeriods.FindAsync(period.Id);
        if (existing == null)
            throw new InvalidOperationException("Período de prematrícula no encontrado");

        if (!SchoolTenantHelper.CanAccessResource(existing.SchoolId, _tenantContext))
            throw new UnauthorizedAccessException("No tiene permisos para modificar períodos de otra escuela.");

        if (period.SchoolId != existing.SchoolId)
            throw new UnauthorizedAccessException("No se puede cambiar la escuela del período.");

        period.UpdatedAt = DateTime.UtcNow;
        period.UpdatedBy = updatedBy;
        
        // Si este es activo, desactivar los demás períodos de la escuela
        if (period.IsActive)
        {
            var existingActive = await _context.PrematriculationPeriods
                .Where(p => p.SchoolId == period.SchoolId && p.IsActive && p.Id != period.Id)
                .ToListAsync();
            
            foreach (var otherPeriod in existingActive)
            {
                otherPeriod.IsActive = false;
                otherPeriod.UpdatedAt = DateTime.UtcNow;
                otherPeriod.UpdatedBy = updatedBy;
            }
        }

        _context.PrematriculationPeriods.Update(period);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Período de prematrícula actualizado: {PeriodId}", period.Id);
        
        return period;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var period = await _context.PrematriculationPeriods.FindAsync(id);
        if (period == null)
            return false;

        if (!SchoolTenantHelper.CanAccessResource(period.SchoolId, _tenantContext))
            throw new UnauthorizedAccessException("No tiene permisos para eliminar períodos de otra escuela.");

        // Verificar que no tenga prematrículas asociadas
        var hasPrematriculations = await _context.Prematriculations
            .AnyAsync(p => p.PrematriculationPeriodId == id);
        
        if (hasPrematriculations)
        {
            _logger.LogWarning("No se puede eliminar el período {PeriodId} porque tiene prematrículas asociadas", id);
            return false;
        }

        _context.PrematriculationPeriods.Remove(period);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Período de prematrícula eliminado: {PeriodId}", id);
        
        return true;
    }

    public async Task<bool> IsPeriodActiveAsync(Guid periodId)
    {
        var now = DateTime.UtcNow;
        return await _context.PrematriculationPeriods
            .AnyAsync(p => p.Id == periodId 
                && p.IsActive 
                && p.StartDate <= now 
                && p.EndDate >= now);
    }
}

