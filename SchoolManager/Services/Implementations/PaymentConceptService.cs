using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class PaymentConceptService : IPaymentConceptService
{
    private readonly SchoolDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PaymentConceptService> _logger;

    public PaymentConceptService(
        SchoolDbContext context,
        ITenantContext tenantContext,
        ILogger<PaymentConceptService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PaymentConcept?> GetByIdAsync(Guid id)
    {
        var concept = await _context.PaymentConcepts
            .Include(pc => pc.CreatedByUser)
            .Include(pc => pc.UpdatedByUser)
            .Include(pc => pc.School)
            .FirstOrDefaultAsync(pc => pc.Id == id);

        if (concept == null)
            return null;

        if (!SchoolTenantHelper.CanAccessResource(concept.SchoolId, _tenantContext))
            return null;

        return concept;
    }

    public async Task<List<PaymentConceptDto>> GetAllAsync(Guid schoolId)
    {
        var concepts = await _context.PaymentConcepts
            .Where(pc => pc.SchoolId == schoolId)
            .Include(pc => pc.CreatedByUser)
            .Include(pc => pc.UpdatedByUser)
            .OrderBy(pc => pc.Name)
            .ToListAsync();

        return concepts.Select(MapToDto).ToList();
    }

    public async Task<List<PaymentConceptDto>> GetActiveAsync(Guid schoolId)
    {
        var concepts = await _context.PaymentConcepts
            .Where(pc => pc.SchoolId == schoolId && pc.IsActive)
            .OrderBy(pc => pc.Name)
            .ToListAsync();

        return concepts.Select(MapToDto).ToList();
    }

    public async Task<PaymentConcept> CreateAsync(PaymentConceptCreateDto dto, Guid createdBy)
    {
        var concept = new PaymentConcept
        {
            Id = Guid.NewGuid(),
            SchoolId = dto.SchoolId,
            Name = dto.Name,
            Description = dto.Description,
            Amount = dto.Amount,
            Periodicity = dto.Periodicity,
            IsActive = dto.IsActive,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentConcepts.Add(concept);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Concepto de pago creado: {ConceptId} - {Name}", concept.Id, concept.Name);

        return concept;
    }

    public async Task<PaymentConcept> UpdateAsync(Guid id, PaymentConceptCreateDto dto, Guid updatedBy)
    {
        var concept = await _context.PaymentConcepts.FindAsync(id);
        if (concept == null)
            throw new Exception("Concepto de pago no encontrado");

        if (!SchoolTenantHelper.CanAccessResource(concept.SchoolId, _tenantContext))
            throw new UnauthorizedAccessException("No tiene permisos para modificar conceptos de otra escuela.");

        if (dto.SchoolId != Guid.Empty && dto.SchoolId != concept.SchoolId)
            throw new UnauthorizedAccessException("No se puede cambiar la escuela del concepto de pago.");

        concept.Name = dto.Name;
        concept.Description = dto.Description;
        concept.Amount = dto.Amount;
        concept.Periodicity = dto.Periodicity;
        concept.IsActive = dto.IsActive;
        concept.UpdatedBy = updatedBy;
        concept.UpdatedAt = DateTime.UtcNow;

        _context.PaymentConcepts.Update(concept);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Concepto de pago actualizado: {ConceptId} - {Name}", concept.Id, concept.Name);

        return concept;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var concept = await _context.PaymentConcepts.FindAsync(id);
        if (concept == null)
            return false;

        if (!SchoolTenantHelper.CanAccessResource(concept.SchoolId, _tenantContext))
            throw new UnauthorizedAccessException("No tiene permisos para eliminar conceptos de otra escuela.");

        // Verificar si hay pagos asociados
        var hasPayments = await _context.Payments.AnyAsync(p => p.PaymentConceptId == id);
        if (hasPayments)
        {
            _logger.LogWarning("No se puede eliminar el concepto {ConceptId} porque tiene pagos asociados", id);
            throw new Exception("No se puede eliminar el concepto porque tiene pagos asociados");
        }

        _context.PaymentConcepts.Remove(concept);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Concepto de pago eliminado: {ConceptId}", id);

        return true;
    }

    private PaymentConceptDto MapToDto(PaymentConcept concept)
    {
        return new PaymentConceptDto
        {
            Id = concept.Id,
            SchoolId = concept.SchoolId,
            Name = concept.Name,
            Description = concept.Description,
            Amount = concept.Amount,
            Periodicity = concept.Periodicity,
            IsActive = concept.IsActive,
            CreatedAt = concept.CreatedAt,
            UpdatedAt = concept.UpdatedAt,
            CreatedByName = concept.CreatedByUser != null 
                ? $"{concept.CreatedByUser.Name} {concept.CreatedByUser.LastName}" 
                : null,
            UpdatedByName = concept.UpdatedByUser != null 
                ? $"{concept.UpdatedByUser.Name} {concept.UpdatedByUser.LastName}" 
                : null
        };
    }
}

