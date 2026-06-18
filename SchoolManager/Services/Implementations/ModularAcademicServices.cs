using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Options;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class CurriculumService : ICurriculumService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CurriculumService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<CurriculumTrack>> GetTracksAsync(Guid? schoolId = null)
    {
        var query = _context.CurriculumTracks
            .Include(t => t.AcademicYear)
            .Include(t => t.CurriculumSubjects)
                .ThenInclude(s => s.Subject)
            .AsNoTracking();

        if (schoolId.HasValue)
            query = query.Where(t => t.SchoolId == null || t.SchoolId == schoolId.Value);

        return await query.OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<CurriculumTrack?> GetActiveTrackAsync(Guid? schoolId, Guid? academicYearId = null)
    {
        var query = _context.CurriculumTracks
            .Include(t => t.CurriculumSubjects)
                .ThenInclude(s => s.Subject)
            .Where(t => t.IsActive);

        if (schoolId.HasValue)
            query = query.Where(t => t.SchoolId == null || t.SchoolId == schoolId.Value);
        else
            query = query.Where(t => t.SchoolId == null);

        if (academicYearId.HasValue)
            query = query.Where(t => t.AcademicYearId == null || t.AcademicYearId == academicYearId.Value);

        return await query
            .OrderByDescending(t => t.SchoolId != null)
            .ThenByDescending(t => t.AcademicYearId != null)
            .ThenByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<CurriculumTrack> CreateTrackAsync(CurriculumTrack track)
    {
        track.Id = track.Id == Guid.Empty ? Guid.NewGuid() : track.Id;
        track.CreatedAt = DateTime.UtcNow;
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        track.CreatedBy = currentUser?.Id;
        if (!track.SchoolId.HasValue)
            track.SchoolId = currentUser?.SchoolId;

        _context.CurriculumTracks.Add(track);
        await _context.SaveChangesAsync();
        return track;
    }

    public async Task<CurriculumSubject> AddSubjectAsync(CurriculumSubject subject)
    {
        subject.Id = subject.Id == Guid.Empty ? Guid.NewGuid() : subject.Id;
        subject.CreatedAt = DateTime.UtcNow;
        if (subject.MinimumPassingScore <= 0)
            subject.MinimumPassingScore = 3.0m;
        if (string.IsNullOrWhiteSpace(subject.LevelName))
        {
            var gradeName = subject.GradeLevelId.HasValue
                ? await _context.GradeLevels.AsNoTracking()
                    .Where(g => g.Id == subject.GradeLevelId.Value)
                    .Select(g => g.Name)
                    .FirstOrDefaultAsync()
                : null;
            subject.LevelName = string.IsNullOrWhiteSpace(gradeName) ? "Sin nivel" : gradeName;
        }

        _context.CurriculumSubjects.Add(subject);
        await _context.SaveChangesAsync();
        return subject;
    }

    public async Task<CurriculumSubjectPrerequisite> AddPrerequisiteAsync(CurriculumSubjectPrerequisite prerequisite)
    {
        if (prerequisite.CurriculumSubjectId == prerequisite.PrerequisiteCurriculumSubjectId)
            throw new InvalidOperationException("Una materia no puede ser prerrequisito de si misma.");

        prerequisite.Id = prerequisite.Id == Guid.Empty ? Guid.NewGuid() : prerequisite.Id;
        prerequisite.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(prerequisite.RequirementType))
            prerequisite.RequirementType = "Required";

        var exists = await _context.CurriculumSubjectPrerequisites.AnyAsync(p =>
            p.CurriculumSubjectId == prerequisite.CurriculumSubjectId &&
            p.PrerequisiteCurriculumSubjectId == prerequisite.PrerequisiteCurriculumSubjectId);
        if (exists)
            throw new InvalidOperationException("El prerrequisito ya existe.");

        _context.CurriculumSubjectPrerequisites.Add(prerequisite);
        await _context.SaveChangesAsync();
        return prerequisite;
    }

    public async Task<CurriculumSubject?> ResolveCurriculumSubjectAsync(Guid schoolId, Guid subjectAssignmentId, Guid? academicYearId = null)
    {
        var subjectAssignment = await _context.SubjectAssignments.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.Id == subjectAssignmentId);
        if (subjectAssignment == null)
            return null;

        var activeTrack = await GetActiveTrackAsync(schoolId, academicYearId);
        if (activeTrack == null)
            return null;

        return await _context.CurriculumSubjects
            .Include(cs => cs.Subject)
            .Where(cs => cs.CurriculumTrackId == activeTrack.Id &&
                         cs.SubjectId == subjectAssignment.SubjectId &&
                         cs.GradeLevelId == subjectAssignment.GradeLevelId &&
                         cs.IsActive)
            .OrderBy(cs => cs.ModuleOrder)
            .FirstOrDefaultAsync();
    }
}

public class AcademicPrerequisiteService : IAcademicPrerequisiteService
{
    private readonly SchoolDbContext _context;
    private readonly IAcademicCreditService _creditService;

    public AcademicPrerequisiteService(SchoolDbContext context, IAcademicCreditService creditService)
    {
        _context = context;
        _creditService = creditService;
    }

    public async Task<PrerequisiteValidationResult> ValidateAsync(Guid studentId, Guid curriculumSubjectId)
    {
        var prerequisites = await _context.CurriculumSubjectPrerequisites
            .Include(p => p.PrerequisiteCurriculumSubject)
                .ThenInclude(s => s.Subject)
            .Where(p => p.CurriculumSubjectId == curriculumSubjectId &&
                        p.IsActive &&
                        p.RequirementType == "Required")
            .ToListAsync();

        if (prerequisites.Count == 0)
            return new PrerequisiteValidationResult(true, "La materia no tiene prerrequisitos obligatorios.", Array.Empty<CurriculumSubject>(), false);

        var missing = new List<CurriculumSubject>();
        foreach (var prerequisite in prerequisites)
        {
            var credit = await _creditService.GetValidCreditAsync(studentId, prerequisite.PrerequisiteCurriculumSubjectId);
            if (credit == null)
                missing.Add(prerequisite.PrerequisiteCurriculumSubject);
        }

        if (missing.Count == 0)
            return new PrerequisiteValidationResult(true, "Prerrequisitos satisfechos.", Array.Empty<CurriculumSubject>(), false);

        var missingIds = missing.Select(m => m.Id).ToList();
        var pendingEquivalence = await _context.StudentSubjectEquivalencyItems
            .AnyAsync(i => missingIds.Contains(i.CurriculumSubjectId) &&
                           i.Status == "Pending" &&
                           i.Equivalency.StudentId == studentId);

        var names = string.Join(", ", missing.Select(m => m.Subject.Name));
        var message = pendingEquivalence
            ? $"Prerrequisitos pendientes de convalidacion: {names}."
            : $"Faltan prerrequisitos aprobados: {names}.";

        return new PrerequisiteValidationResult(false, message, missing, pendingEquivalence);
    }
}

public class AcademicCreditService : IAcademicCreditService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AcademicCreditService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<StudentAcademicCredit?> GetValidCreditAsync(Guid studentId, Guid curriculumSubjectId)
    {
        return await _context.StudentAcademicCredits.AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.StudentId == studentId &&
                c.CurriculumSubjectId == curriculumSubjectId &&
                c.Status == "Valid");
    }

    public async Task<StudentAcademicCredit> CreateCreditAsync(StudentAcademicCredit credit)
    {
        var existing = await _context.StudentAcademicCredits.FirstOrDefaultAsync(c =>
            c.StudentId == credit.StudentId &&
            c.CurriculumSubjectId == credit.CurriculumSubjectId &&
            c.Status == "Valid");
        if (existing != null)
            return existing;

        credit.Id = credit.Id == Guid.Empty ? Guid.NewGuid() : credit.Id;
        credit.CreatedAt = DateTime.UtcNow;
        if (credit.ApprovedAt == default)
            credit.ApprovedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(credit.Status))
            credit.Status = "Valid";

        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        credit.CreatedBy = currentUserId;
        await AuditHelper.SetSchoolIdAsync(credit, _currentUserService);

        _context.StudentAcademicCredits.Add(credit);
        await _context.SaveChangesAsync();
        return credit;
    }

    public async Task<StudentAcademicCredit?> CreateFromPromotionAsync(SubjectPromotionRecord promotionRecord)
    {
        if (!promotionRecord.CurriculumSubjectId.HasValue ||
            !promotionRecord.Outcome.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await CreateCreditAsync(new StudentAcademicCredit
        {
            StudentId = promotionRecord.StudentId,
            CurriculumSubjectId = promotionRecord.CurriculumSubjectId.Value,
            SubjectId = promotionRecord.SubjectId,
            GradeLevelId = promotionRecord.GradeLevelId,
            AcademicYearId = promotionRecord.AcademicYearId,
            TrimesterId = promotionRecord.TrimesterId,
            SourceType = "Promotion",
            SourceId = promotionRecord.Id,
            FinalScore = promotionRecord.FinalScore,
            ApprovedAt = promotionRecord.PromotedAt,
            Status = "Valid"
        });
    }
}

public class ModularEnrollmentService : IModularEnrollmentService
{
    private readonly SchoolDbContext _context;
    private readonly NocturnalModularEnrollmentOptions _options;
    private readonly IAcademicYearService _academicYearService;
    private readonly ICurriculumService _curriculumService;
    private readonly IAcademicPrerequisiteService _prerequisiteService;
    private readonly ICurrentUserService _currentUserService;

    public ModularEnrollmentService(
        SchoolDbContext context,
        IOptions<NocturnalModularEnrollmentOptions> options,
        IAcademicYearService academicYearService,
        ICurriculumService curriculumService,
        IAcademicPrerequisiteService prerequisiteService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _options = options.Value;
        _academicYearService = academicYearService;
        _curriculumService = curriculumService;
        _prerequisiteService = prerequisiteService;
        _currentUserService = currentUserService;
    }

    public Task<bool> IsEnabledForSchoolAsync(Guid? schoolId)
    {
        if (!_options.Enabled)
            return Task.FromResult(false);
        if (!schoolId.HasValue || schoolId.Value == Guid.Empty)
            return Task.FromResult(true);
        if (_options.EnabledSchoolIds.Count == 0)
            return Task.FromResult(true);

        return Task.FromResult(_options.EnabledSchoolIds.Any(id =>
            Guid.TryParse(id, out var parsed) && parsed == schoolId.Value));
    }

    public async Task<StudentAcademicPeriodEnrollment?> EnsurePeriodEnrollmentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid trimesterId,
        Guid? studentAssignmentId,
        string? entryType)
    {
        var student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId);
        if (student?.SchoolId == null)
            return null;

        var existing = await _context.StudentAcademicPeriodEnrollments.FirstOrDefaultAsync(e =>
            e.StudentId == studentId &&
            e.AcademicYearId == academicYearId &&
            e.TrimesterId == trimesterId &&
            e.Status != "Cancelled");
        if (existing != null)
            return existing;

        var enrollment = new StudentAcademicPeriodEnrollment
        {
            Id = Guid.NewGuid(),
            SchoolId = student.SchoolId.Value,
            StudentId = studentId,
            AcademicYearId = academicYearId,
            TrimesterId = trimesterId,
            StudentAssignmentId = studentAssignmentId,
            EntryType = string.IsNullOrWhiteSpace(entryType) ? "Regular" : entryType.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = await _currentUserService.GetCurrentUserIdAsync()
        };

        _context.StudentAcademicPeriodEnrollments.Add(enrollment);
        await _context.SaveChangesAsync();
        return enrollment;
    }

    public async Task<ModularSubjectEnrollmentResult> EnrollSubjectAsync(ModularSubjectEnrollmentRequest request)
    {
        if (request.StudentId == Guid.Empty || request.SubjectAssignmentId == Guid.Empty)
            return new ModularSubjectEnrollmentResult(true, false, "Datos invalidos.", null);

        var student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.StudentId);
        if (student?.SchoolId == null || !await IsEnabledForSchoolAsync(student.SchoolId))
            return new ModularSubjectEnrollmentResult(false, false, "Modelo modular no activo para la escuela.", null);

        var academicYear = await _academicYearService.GetActiveAcademicYearAsync(student.SchoolId.Value);
        var curriculumSubject = await _curriculumService.ResolveCurriculumSubjectAsync(
            student.SchoolId.Value,
            request.SubjectAssignmentId,
            academicYear?.Id);

        if (curriculumSubject == null)
            return new ModularSubjectEnrollmentResult(false, false, "No hay malla modular activa para esta materia; usar flujo legacy.", null);

        if (!request.TrimesterId.HasValue || request.TrimesterId.Value == Guid.Empty)
            return new ModularSubjectEnrollmentResult(true, false, "Debe seleccionar un trimestre para la matricula modular.", null);

        var trimester = await _context.Trimesters.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TrimesterId.Value && t.SchoolId == student.SchoolId);
        if (trimester == null)
            return new ModularSubjectEnrollmentResult(true, false, "Trimestre no valido para la escuela del estudiante.", null);

        var validation = await _prerequisiteService.ValidateAsync(request.StudentId, curriculumSubject.Id);
        if (!validation.CanEnroll)
        {
            var status = validation.PendingEquivalence ? "PendingEquivalence" : "Blocked";
            return new ModularSubjectEnrollmentResult(true, false, $"{status}: {validation.Message}", null);
        }

        var subjectAssignment = await _context.SubjectAssignments.AsNoTracking()
            .FirstAsync(sa => sa.Id == request.SubjectAssignmentId);
        var group = await _context.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == subjectAssignment.GroupId);

        var baseAssignment = await _context.StudentAssignments
            .Where(sa => sa.StudentId == request.StudentId &&
                         sa.IsActive &&
                         sa.GradeId == subjectAssignment.GradeLevelId &&
                         sa.GroupId == subjectAssignment.GroupId &&
                         sa.ShiftId == group!.ShiftId)
            .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
            .FirstOrDefaultAsync();

        if (baseAssignment == null)
        {
            baseAssignment = new StudentAssignment
            {
                Id = Guid.NewGuid(),
                StudentId = request.StudentId,
                GradeId = subjectAssignment.GradeLevelId,
                GroupId = subjectAssignment.GroupId,
                ShiftId = group?.ShiftId,
                IsActive = true,
                AcademicYearId = academicYear?.Id,
                EnrollmentType = request.AsCarryOver ? EnrollmentTypeConstants.Refuerzo : EnrollmentTypeConstants.DefaultPrimary,
                StartDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _context.StudentAssignments.Add(baseAssignment);
            await _context.SaveChangesAsync();
        }

        var exists = await _context.StudentSubjectAssignments.AnyAsync(ssa =>
            ssa.StudentId == request.StudentId &&
            ssa.SubjectAssignmentId == request.SubjectAssignmentId &&
            ssa.AcademicYearId == baseAssignment.AcademicYearId &&
            ssa.TrimesterId == request.TrimesterId &&
            ssa.IsActive);
        if (exists)
            return new ModularSubjectEnrollmentResult(true, false, "La materia ya esta matriculada para ese trimestre.", null);

        if (!baseAssignment.AcademicYearId.HasValue)
            return new ModularSubjectEnrollmentResult(true, false, "No se pudo resolver el ano academico activo.", null);

        var periodEnrollment = await EnsurePeriodEnrollmentAsync(
            request.StudentId,
            baseAssignment.AcademicYearId.Value,
            request.TrimesterId.Value,
            baseAssignment.Id,
            request.EntryType);

        var enrollmentType = request.AsCarryOver
            ? EnrollmentTypeConstants.Refuerzo
            : EnrollmentTypeConstants.NormalizePrimary(baseAssignment.EnrollmentType);

        var enrollment = new StudentSubjectAssignment
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            SubjectAssignmentId = request.SubjectAssignmentId,
            StudentAssignmentId = baseAssignment.Id,
            AcademicYearId = baseAssignment.AcademicYearId,
            ShiftId = baseAssignment.ShiftId,
            PeriodEnrollmentId = periodEnrollment?.Id,
            TrimesterId = request.TrimesterId,
            CurriculumSubjectId = curriculumSubject.Id,
            EnrollmentType = enrollmentType,
            Status = "Active",
            IsActive = true,
            ValidationStatus = "Validated",
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await AuditHelper.SetAuditFieldsForCreateAsync(enrollment, _currentUserService);
        await AuditHelper.SetSchoolIdAsync(enrollment, _currentUserService);

        _context.StudentSubjectAssignments.Add(enrollment);
        await _context.SaveChangesAsync();
        return new ModularSubjectEnrollmentResult(true, true, "Materia modular matriculada correctamente.", enrollment.Id);
    }
}

public class EquivalencyService : IEquivalencyService
{
    private readonly SchoolDbContext _context;
    private readonly IAcademicCreditService _creditService;
    private readonly ICurrentUserService _currentUserService;

    public EquivalencyService(
        SchoolDbContext context,
        IAcademicCreditService creditService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _creditService = creditService;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<StudentSubjectEquivalency>> GetPendingAsync()
    {
        return await _context.StudentSubjectEquivalencies
            .Include(e => e.Student)
            .Include(e => e.Items)
                .ThenInclude(i => i.CurriculumSubject)
                    .ThenInclude(s => s.Subject)
            .Where(e => e.Status == "Pending" || e.Items.Any(i => i.Status == "Pending"))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<StudentSubjectEquivalency> CreateAsync(StudentSubjectEquivalency equivalency)
    {
        equivalency.Id = equivalency.Id == Guid.Empty ? Guid.NewGuid() : equivalency.Id;
        equivalency.CreatedAt = DateTime.UtcNow;
        equivalency.Status = string.IsNullOrWhiteSpace(equivalency.Status) ? "Pending" : equivalency.Status;
        equivalency.CreatedBy = await _currentUserService.GetCurrentUserIdAsync();
        _context.StudentSubjectEquivalencies.Add(equivalency);
        await _context.SaveChangesAsync();
        return equivalency;
    }

    public async Task<StudentSubjectEquivalencyItem> AddItemAsync(StudentSubjectEquivalencyItem item)
    {
        item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
        item.CreatedAt = DateTime.UtcNow;
        item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Pending" : item.Status;
        _context.StudentSubjectEquivalencyItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<StudentAcademicCredit?> ApproveItemAsync(Guid itemId, Guid reviewedBy)
    {
        var item = await _context.StudentSubjectEquivalencyItems
            .Include(i => i.Equivalency)
            .Include(i => i.CurriculumSubject)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
            return null;

        item.Status = "Approved";
        item.UpdatedAt = DateTime.UtcNow;
        item.Equivalency.Status = "Approved";
        item.Equivalency.ReviewedBy = reviewedBy;
        item.Equivalency.ReviewedAt = DateTime.UtcNow;

        var credit = await _creditService.CreateCreditAsync(new StudentAcademicCredit
        {
            SchoolId = item.Equivalency.SchoolId,
            StudentId = item.Equivalency.StudentId,
            CurriculumSubjectId = item.CurriculumSubjectId,
            SubjectId = item.CurriculumSubject.SubjectId,
            GradeLevelId = item.CurriculumSubject.GradeLevelId,
            SourceType = "Equivalence",
            SourceId = item.EquivalencyId,
            FinalScore = item.NormalizedScore,
            ApprovedAt = DateTime.UtcNow,
            Status = "Valid",
            Notes = $"Convalidacion: {item.ExternalSubjectName}"
        });

        await _context.SaveChangesAsync();
        return credit;
    }

    public async Task RejectItemAsync(Guid itemId, Guid reviewedBy)
    {
        var item = await _context.StudentSubjectEquivalencyItems
            .Include(i => i.Equivalency)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
            return;

        item.Status = "Rejected";
        item.UpdatedAt = DateTime.UtcNow;
        item.Equivalency.ReviewedBy = reviewedBy;
        item.Equivalency.ReviewedAt = DateTime.UtcNow;
        if (!item.Equivalency.Items.Any(i => i.Id != itemId && i.Status == "Pending"))
            item.Equivalency.Status = "Rejected";
        await _context.SaveChangesAsync();
    }
}

public class ModularPromotionService : IModularPromotionService
{
    private readonly SchoolDbContext _context;
    private readonly IAcademicCreditService _creditService;
    private readonly ICurrentUserService _currentUserService;

    public ModularPromotionService(
        SchoolDbContext context,
        IAcademicCreditService creditService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _creditService = creditService;
        _currentUserService = currentUserService;
    }

    public async Task<ModularPromotionResult> PromoteAsync(
        Guid studentId,
        Guid studentSubjectAssignmentId,
        Guid? trimesterId,
        string trimesterCode,
        decimal? finalScore,
        string? outcome)
    {
        var ssa = await _context.StudentSubjectAssignments
            .Include(x => x.SubjectAssignment)
            .FirstOrDefaultAsync(x => x.Id == studentSubjectAssignmentId && x.StudentId == studentId && x.IsActive);
        if (ssa == null)
            return new ModularPromotionResult(false, "Inscripcion de materia no encontrada o inactiva.", null, null);

        var normalizedOutcome = string.IsNullOrWhiteSpace(outcome)
            ? finalScore.HasValue && finalScore.Value >= 3.0m ? "Approved" : "Failed"
            : outcome.Trim();

        var record = new SubjectPromotionRecord
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            SubjectId = ssa.SubjectAssignment.SubjectId,
            GradeLevelId = ssa.SubjectAssignment.GradeLevelId,
            AcademicYearId = ssa.AcademicYearId,
            TrimesterId = trimesterId ?? ssa.TrimesterId,
            CurriculumSubjectId = ssa.CurriculumSubjectId,
            Trimester = string.IsNullOrWhiteSpace(trimesterCode) ? "" : trimesterCode.Trim(),
            Outcome = normalizedOutcome,
            FinalScore = finalScore,
            StudentSubjectAssignmentId = ssa.Id,
            PromotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await AuditHelper.SetSchoolIdAsync(record, _currentUserService);
        record.CreatedBy = await _currentUserService.GetCurrentUserIdAsync();

        StudentAcademicCredit? credit = null;
        if (normalizedOutcome.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            ssa.Status = "Approved";
            ssa.IsActive = false;
            ssa.EndDate = DateTime.UtcNow;
        }
        else if (normalizedOutcome.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            ssa.Status = "Failed";
            if (!EnrollmentTypeConstants.IsCarryOver(ssa.EnrollmentType))
                ssa.EnrollmentType = EnrollmentTypeConstants.Refuerzo;
        }

        _context.SubjectPromotionRecords.Add(record);
        await _context.SaveChangesAsync();

        if (normalizedOutcome.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            credit = await _creditService.CreateFromPromotionAsync(record);
            if (credit != null)
            {
                record.AcademicCreditId = credit.Id;
                await _context.SaveChangesAsync();
            }
        }

        return new ModularPromotionResult(true, "Promocion modular registrada correctamente.", record.Id, credit?.Id);
    }
}
