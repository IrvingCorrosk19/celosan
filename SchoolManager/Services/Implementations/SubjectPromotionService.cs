using Microsoft.EntityFrameworkCore;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class SubjectPromotionService : ISubjectPromotionService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAcademicYearService _academicYearService;

    public SubjectPromotionService(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        IAcademicYearService academicYearService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _academicYearService = academicYearService;
    }

    public async Task<(bool Success, string Message)> PromoteSubjectAsync(
        Guid studentId,
        Guid studentSubjectAssignmentId,
        string trimester,
        decimal? finalScore,
        string outcome)
    {
        if (studentId == Guid.Empty || studentSubjectAssignmentId == Guid.Empty)
            return (false, "Datos inválidos.");

        var ssa = await _context.StudentSubjectAssignments
            .Include(x => x.SubjectAssignment)
            .FirstOrDefaultAsync(x => x.Id == studentSubjectAssignmentId && x.StudentId == studentId && x.IsActive);

        if (ssa == null)
            return (false, "Inscripción de materia no encontrada o inactiva.");

        var student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId);
        var academicYear = student?.SchoolId.HasValue == true
            ? await _academicYearService.GetActiveAcademicYearAsync(student.SchoolId.Value)
            : null;

        var normalizedOutcome = (outcome ?? "").Trim();
        if (string.IsNullOrEmpty(normalizedOutcome))
            normalizedOutcome = finalScore.HasValue && finalScore.Value >= 3.0m ? "Approved" : "Failed";

        var record = new SubjectPromotionRecord
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            SubjectId = ssa.SubjectAssignment.SubjectId,
            GradeLevelId = ssa.SubjectAssignment.GradeLevelId,
            AcademicYearId = ssa.AcademicYearId ?? academicYear?.Id,
            Trimester = trimester.Trim(),
            Outcome = normalizedOutcome,
            FinalScore = finalScore,
            StudentSubjectAssignmentId = ssa.Id,
            PromotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await AuditHelper.SetSchoolIdAsync(record, _currentUserService);
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (currentUserId.HasValue)
            record.CreatedBy = currentUserId.Value;

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

        return (true, "Promoción registrada correctamente.");
    }

    public async Task<(bool Success, string Message, int Processed)> CloseYearForStudentAsync(
        Guid studentId, string trimester, decimal passingScore = 3.0m)
    {
        var enrollments = await _context.StudentSubjectAssignments
            .Include(x => x.SubjectAssignment)
            .Where(x => x.StudentId == studentId && x.IsActive)
            .ToListAsync();

        if (enrollments.Count == 0)
            return (false, "El estudiante no tiene materias activas.", 0);

        var processed = 0;
        foreach (var ssa in enrollments)
        {
            var avg = await (
                from s in _context.StudentActivityScores
                join a in _context.Activities on s.ActivityId equals a.Id
                where s.StudentSubjectAssignmentId == ssa.Id &&
                      a.Trimester == trimester &&
                      s.Score.HasValue
                select s.Score!.Value).DefaultIfEmpty().AverageAsync();

            var outcome = avg >= passingScore ? "Approved" : "Failed";
            var result = await PromoteSubjectAsync(studentId, ssa.Id, trimester, avg, outcome);
            if (result.Success)
                processed++;
        }

        return (true, $"Cierre procesado para {processed} materia(s).", processed);
    }

    public async Task<IReadOnlyList<SubjectPromotionRecordDto>> GetRecordsByStudentAsync(
        Guid studentId, Guid? academicYearId = null)
    {
        var query = _context.SubjectPromotionRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId);

        if (academicYearId.HasValue)
            query = query.Where(r => r.AcademicYearId == academicYearId);

        return await query
            .Join(_context.Subjects.AsNoTracking(), r => r.SubjectId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.GradeLevels.AsNoTracking(), x => x.r.GradeLevelId, g => g.Id, (x, g) => new SubjectPromotionRecordDto
            {
                Id = x.r.Id,
                SubjectName = x.s.Name,
                GradeName = g.Name,
                Trimester = x.r.Trimester,
                Outcome = x.r.Outcome,
                FinalScore = x.r.FinalScore,
                PromotedAt = x.r.PromotedAt
            })
            .OrderByDescending(x => x.PromotedAt)
            .ToListAsync();
    }
}
