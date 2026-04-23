using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

/// <summary>Implementación del servicio Club de Padres. No modifica StudentIdCardService, PaymentService ni AuthService.</summary>
public class ClubParentsPaymentService : IClubParentsPaymentService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ClubParentsPaymentService> _logger;

    private const string CarnetPendiente = "Pendiente";
    private const string CarnetPagado = "Pagado";
    private const string PlatformPendiente = "Pendiente";
    private const string PlatformActivo = "Activo";

    public ClubParentsPaymentService(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ClubParentsPaymentService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClubParentsStudentDto>> GetStudentsAsync(
        Guid? gradeId = null,
        Guid? groupId = null,
        string? search = null,
        string? cedula = null)
    {
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
        {
            _logger.LogWarning("[ClubParents] GetStudentsAsync: usuario sin escuela.");
            return Array.Empty<ClubParentsStudentDto>();
        }

        _logger.LogInformation("[ClubParents] GetStudentsAsync querying Users: Role IN (student,estudiante) AND (User.SchoolId={SchoolId} OR asignación activa al colegio)", school.Id);

        // Incluir alumnos con users.school_id = escuela O sin school_id en user pero con matrícula activa (grado del colegio).
        var query = _context.Users
            .Where(u => u.Role != null && (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante"))
            .Where(u => u.SchoolId == school.Id
                || u.StudentAssignments.Any(sa => sa.IsActive && sa.Grade.SchoolId == school.Id));

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var t = term.ToLower();
            query = query.Where(u =>
                (u.DocumentId != null && u.DocumentId.ToLower().Contains(t))
                || u.Name.ToLower().Contains(t)
                || u.LastName.ToLower().Contains(t)
                || (u.Name + " " + u.LastName).ToLower().Contains(t));
        }

        var cedulaFilter = cedula?.Trim();
        if (!string.IsNullOrEmpty(cedulaFilter))
        {
            var likeCed = "%" + cedulaFilter + "%";
            var digitsOnlyCed = new string(cedulaFilter.Where(char.IsDigit).ToArray());
            query = query.Where(u =>
                u.DocumentId != null
                && (EF.Functions.ILike(u.DocumentId, likeCed)
                    || (digitsOnlyCed.Length > 0
                        && u.DocumentId!.Replace(".", "").Replace("-", "").Replace(" ", "").Contains(digitsOnlyCed))));
        }

        if (gradeId.HasValue || groupId.HasValue)
        {
            query = query.Where(u => u.StudentAssignments.Any(sa =>
                sa.IsActive
                && (!gradeId.HasValue || sa.GradeId == gradeId.Value)
                && (!groupId.HasValue || sa.GroupId == groupId.Value)));
        }

        // Proyección en la consulta (como en StudentIdCard list-json) para evitar múltiples Include y posibles 500
        var list = await query
            .Select(u => new
            {
                u.Id,
                FullName = u.Name + " " + u.LastName,
                u.DocumentId,
                Grade = u.StudentAssignments.Where(sa => sa.IsActive)
                    .OrderByDescending(sa => gradeId.HasValue && sa.GradeId == gradeId.Value)
                    .ThenByDescending(sa => groupId.HasValue && sa.GroupId == groupId.Value)
                    .ThenByDescending(sa => sa.EnrollmentType != null && sa.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(sa => sa.Shift != null && sa.Shift.Name != null && sa.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
                    .Select(sa => sa.Grade.Name)
                    .FirstOrDefault() ?? "Sin asignar",
                Group = u.StudentAssignments.Where(sa => sa.IsActive)
                    .OrderByDescending(sa => gradeId.HasValue && sa.GradeId == gradeId.Value)
                    .ThenByDescending(sa => groupId.HasValue && sa.GroupId == groupId.Value)
                    .ThenByDescending(sa => sa.EnrollmentType != null && sa.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(sa => sa.Shift != null && sa.Shift.Name != null && sa.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
                    .Select(sa => sa.Group.Name)
                    .FirstOrDefault() ?? "Sin asignar"
            })
            .ToListAsync();

        _logger.LogInformation("[ClubParents] GetStudentsAsync found {Count} users for SchoolId={SchoolId}", list.Count, school.Id);

        if (list.Count == 0)
            return Array.Empty<ClubParentsStudentDto>();

        var studentIds = list.Select(x => x.Id).ToList();
        var accessByStudent = await _context.StudentPaymentAccesses
            .Where(a => a.SchoolId == school.Id && studentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, a => a);

        var result = list.Select(x =>
        {
            var access = accessByStudent.GetValueOrDefault(x.Id);
            return new ClubParentsStudentDto
            {
                Id = x.Id,
                FullName = x.FullName ?? "",
                DocumentId = x.DocumentId,
                Grade = x.Grade ?? "Sin asignar",
                Group = x.Group ?? "Sin asignar",
                CarnetStatus = access?.CarnetStatus ?? CarnetPendiente,
                PlatformAccessStatus = access?.PlatformAccessStatus ?? PlatformPendiente
            };
        }).OrderBy(x => x.FullName).ToList();

        return result;
    }

    /// <inheritdoc />
    public async Task<StudentPaymentStatusDto> GetStudentPaymentStatusAsync(Guid studentId)
    {
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
            return new StudentPaymentStatusDto { StudentId = studentId, CarnetStatus = CarnetPendiente, PlatformAccessStatus = PlatformPendiente };

        var access = await _context.StudentPaymentAccesses
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.SchoolId == school.Id);

        if (access == null)
            return new StudentPaymentStatusDto
            {
                StudentId = studentId,
                CarnetStatus = CarnetPendiente,
                PlatformAccessStatus = PlatformPendiente
            };

        return new StudentPaymentStatusDto
        {
            StudentId = studentId,
            CarnetStatus = access.CarnetStatus,
            PlatformAccessStatus = access.PlatformAccessStatus,
            CarnetStatusUpdatedAt = access.CarnetStatusUpdatedAt,
            PlatformStatusUpdatedAt = access.PlatformStatusUpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task MarkCarnetAsPaidAsync(Guid studentId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
            throw new InvalidOperationException("Usuario sin escuela asignada.");

        var student = await FindStudentInSchoolAsync(studentId, school.Id);
        if (student == null)
            throw new InvalidOperationException("Estudiante no encontrado o no pertenece a su escuela.");

        var access = await _context.StudentPaymentAccesses
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.SchoolId == school.Id);

        if (access == null)
        {
            access = new StudentPaymentAccess
            {
                StudentId = studentId,
                SchoolId = school.Id,
                CarnetStatus = CarnetPendiente,
                PlatformAccessStatus = PlatformPendiente,
                CreatedAt = DateTime.UtcNow
            };
            _context.StudentPaymentAccesses.Add(access);
        }

        if (access.CarnetStatus != CarnetPendiente)
            throw new InvalidOperationException($"Solo se puede marcar como Pagado desde estado Pendiente. Estado actual: {access.CarnetStatus}.");

        var now = DateTime.UtcNow;
        access.CarnetStatus = CarnetPagado;
        access.CarnetStatusUpdatedAt = now;
        access.CarnetUpdatedByUserId = userId;
        access.UpdatedAt = now;

        await _context.SaveChangesAsync();
        _logger.LogInformation("[ClubParents] Carnet marcado como Pagado StudentId={StudentId} por UserId={UserId}", studentId, userId);
    }

    /// <inheritdoc />
    public async Task ActivatePlatformAsync(Guid studentId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (school == null)
            throw new InvalidOperationException("Usuario sin escuela asignada.");

        var student = await FindStudentInSchoolAsync(studentId, school.Id);
        if (student == null)
            throw new InvalidOperationException("Estudiante no encontrado o no pertenece a su escuela.");

        var access = await _context.StudentPaymentAccesses
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.SchoolId == school.Id);

        if (access == null)
        {
            access = new StudentPaymentAccess
            {
                StudentId = studentId,
                SchoolId = school.Id,
                CarnetStatus = CarnetPendiente,
                PlatformAccessStatus = PlatformPendiente,
                CreatedAt = DateTime.UtcNow
            };
            _context.StudentPaymentAccesses.Add(access);
        }

        if (access.PlatformAccessStatus != PlatformPendiente)
            throw new InvalidOperationException($"Solo se puede activar plataforma desde estado Pendiente. Estado actual: {access.PlatformAccessStatus}.");

        var now = DateTime.UtcNow;
        access.PlatformAccessStatus = PlatformActivo;
        access.PlatformStatusUpdatedAt = now;
        access.PlatformUpdatedByUserId = userId;
        access.UpdatedAt = now;

        await _context.SaveChangesAsync();
        _logger.LogInformation("[ClubParents] Plataforma activada StudentId={StudentId} por UserId={UserId}", studentId, userId);
    }

    private Task<User?> FindStudentInSchoolAsync(Guid studentId, Guid schoolId) =>
        _context.Users.FirstOrDefaultAsync(u =>
            u.Id == studentId
            && u.Role != null
            && (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante")
            && (u.SchoolId == schoolId
                || u.StudentAssignments.Any(sa => sa.IsActive && sa.Grade.SchoolId == schoolId)));
}
