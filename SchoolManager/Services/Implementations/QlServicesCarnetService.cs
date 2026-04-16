using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

/// <summary>Implementación del servicio QL Services: carnets pendientes de impresión, marcar Impreso/Entregado.</summary>
public class QlServicesCarnetService : IQlServicesCarnetService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QlServicesCarnetService> _logger;

    private const string CarnetPagado = "Pagado";
    private const string CarnetImpreso = "Impreso";
    private const string CarnetEntregado = "Entregado";

    public QlServicesCarnetService(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        ILogger<QlServicesCarnetService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingPrintItemDto>> GetPendingPrintAsync()
    {
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
        {
            _logger.LogWarning("[QlServices] GetPendingPrintAsync: usuario sin escuela.");
            return Array.Empty<PendingPrintItemDto>();
        }

        var list = await _context.StudentPaymentAccesses
            .Where(a => a.SchoolId == school.Id && a.CarnetStatus == CarnetPagado)
            .Include(a => a.Student)
                .ThenInclude(s => s.StudentAssignments.Where(sa => sa.IsActive))
                    .ThenInclude(sa => sa.Grade)
            .Include(a => a.Student)
                .ThenInclude(s => s.StudentAssignments.Where(sa => sa.IsActive))
                    .ThenInclude(sa => sa.Group)
            .OrderBy(a => a.CarnetStatusUpdatedAt)
            .ToListAsync();

        return list.Select(a =>
        {
            var active = ActiveStudentAssignmentHelper.PickForDisplay(a.Student.StudentAssignments);
            return new PendingPrintItemDto
            {
                StudentId = a.StudentId,
                FullName = $"{a.Student.Name} {a.Student.LastName}",
                Grade = active?.Grade?.Name ?? "Sin asignar",
                Group = active?.Group?.Name ?? "Sin asignar",
                CarnetStatus = a.CarnetStatus,
                CarnetStatusUpdatedAt = a.CarnetStatusUpdatedAt
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task MarkCarnetAsPrintedAsync(Guid studentId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
            throw new InvalidOperationException("Usuario sin escuela asignada.");

        var access = await _context.StudentPaymentAccesses
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.SchoolId == school.Id);
        if (access == null)
            throw new InvalidOperationException("No existe registro de pago para este estudiante en su escuela.");
        if (access.CarnetStatus != CarnetPagado)
            throw new InvalidOperationException($"Solo se puede marcar Impreso desde Pagado. Estado actual: {access.CarnetStatus}.");

        var now = DateTime.UtcNow;
        access.CarnetStatus = CarnetImpreso;
        access.CarnetStatusUpdatedAt = now;
        access.CarnetUpdatedByUserId = userId;
        access.UpdatedAt = now;

        await _context.SaveChangesAsync();
        _logger.LogInformation("[QlServices] Carnet marcado Impreso StudentId={StudentId} por UserId={UserId}", studentId, userId);
    }

    /// <inheritdoc />
    public async Task MarkCarnetAsDeliveredAsync(Guid studentId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
            throw new InvalidOperationException("Usuario sin escuela asignada.");

        var access = await _context.StudentPaymentAccesses
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.SchoolId == school.Id);
        if (access == null)
            throw new InvalidOperationException("No existe registro de pago para este estudiante en su escuela.");
        if (access.CarnetStatus != CarnetImpreso)
            throw new InvalidOperationException($"Solo se puede marcar Entregado desde Impreso. Estado actual: {access.CarnetStatus}.");

        var now = DateTime.UtcNow;
        access.CarnetStatus = CarnetEntregado;
        access.CarnetStatusUpdatedAt = now;
        access.CarnetUpdatedByUserId = userId;
        access.UpdatedAt = now;

        await _context.SaveChangesAsync();
        _logger.LogInformation("[QlServices] Carnet marcado Entregado StudentId={StudentId} por UserId={UserId}", studentId, userId);
    }
}
