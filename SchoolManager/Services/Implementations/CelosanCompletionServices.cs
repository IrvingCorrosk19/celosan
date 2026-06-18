using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using SchoolManager.Interfaces;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class StudentIdentityDocumentService : IStudentIdentityDocumentService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorage _fileStorage;

    public StudentIdentityDocumentService(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        IFileStorage fileStorage)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
    }

    public async Task<StudentIdentityDocument?> GetLatestAsync(Guid studentId)
    {
        return await _context.StudentIdentityDocuments
            .Include(d => d.Student)
            .AsNoTracking()
            .Where(d => d.StudentId == studentId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<StudentIdentityDocument> SaveAsync(Guid studentId, string? documentNumber, DateTime? expirationDate, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Debe adjuntar el archivo de cedula.");

        var student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId);
        if (student?.SchoolId == null)
            throw new InvalidOperationException("Estudiante no encontrado o sin escuela.");

        var ext = Path.GetExtension(file.FileName);
        var path = $"identity-documents/{studentId:N}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveAsync(path, stream);
        var now = DateTime.UtcNow;
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        var status = expirationDate.HasValue && expirationDate.Value.Date < now.Date ? "Expired" : "Valid";

        var document = new StudentIdentityDocument
        {
            Id = Guid.NewGuid(),
            SchoolId = student.SchoolId.Value,
            StudentId = studentId,
            DocumentType = "Cedula",
            DocumentNumber = string.IsNullOrWhiteSpace(documentNumber) ? student.DocumentId : documentNumber.Trim(),
            FileUrl = url,
            ExpirationDate = expirationDate?.ToUniversalTime(),
            Status = status,
            CreatedAt = now,
            CreatedBy = currentUserId
        };

        _context.StudentIdentityDocuments.Add(document);
        await AddAuditAsync(student.SchoolId.Value, currentUserId, "IdentityDocument.Updated", "StudentIdentityDocument", $"Documento de identidad actualizado para {student.Email}. Estado: {status}");
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<IReadOnlyList<StudentIdentityDocument>> GetExpiredOrMissingAsync(Guid? schoolId = null)
    {
        var now = DateTime.UtcNow.Date;
        var query = _context.StudentIdentityDocuments
            .Include(d => d.Student)
            .AsNoTracking()
            .Where(d => d.Status == "Expired" || (d.ExpirationDate.HasValue && d.ExpirationDate.Value.Date < now));

        if (schoolId.HasValue)
            query = query.Where(d => d.SchoolId == schoolId.Value);

        return await query.OrderBy(d => d.ExpirationDate).ToListAsync();
    }

    private async Task AddAuditAsync(Guid schoolId, Guid? userId, string action, string resource, string details)
    {
        var user = userId.HasValue ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value) : null;
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = userId,
            UserName = user == null ? null : $"{user.Name} {user.LastName}",
            UserRole = user?.Role,
            Action = action,
            Resource = resource,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class CelosanBulkImportService : ICelosanBulkImportService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CelosanBulkImportService(
        SchoolDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CelosanBulkImportResult> ImportApprovedCreditsAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return new CelosanBulkImportResult(false, 0, 0, 0, new[] { "Debe adjuntar un CSV." });

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return new CelosanBulkImportResult(false, 0, 0, 0, new[] { "No se pudo resolver la escuela actual." });

        var errors = new List<string>();
        var processed = 0;
        var success = 0;

        using var reader = new StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync();
        var headers = SplitCsv(header ?? string.Empty).Select(h => h.Trim().ToLowerInvariant()).ToList();
        var emailIdx = headers.IndexOf("email");
        var subjectIdx = headers.IndexOf("materia");
        var levelIdx = headers.IndexOf("nivel");
        var scoreIdx = headers.IndexOf("nota");
        if (emailIdx < 0 || subjectIdx < 0 || levelIdx < 0)
        {
            errors.Add("CSV requerido: email,materia,nivel,nota");
            await LogAsync(currentUser, file.FileName, processed, success, errors);
            return new CelosanBulkImportResult(false, processed, success, errors.Count, errors);
        }

        var rows = new List<(int RowNumber, string Email, string SubjectName, string LevelName, string? ScoreText)>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            processed++;
            var columns = SplitCsv(line).ToList();
            rows.Add((
                processed + 1,
                Get(columns, emailIdx),
                Get(columns, subjectIdx),
                Get(columns, levelIdx),
                scoreIdx >= 0 ? Get(columns, scoreIdx) : null));
        }

        var normalizedEmails = rows
            .Select(r => NormalizeKey(r.Email))
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToList();
        var students = await _context.Users.AsNoTracking()
            .Where(u => u.SchoolId == currentUser.SchoolId && normalizedEmails.Contains(u.Email.ToLower()))
            .ToDictionaryAsync(u => NormalizeKey(u.Email), u => u);

        var curriculumSubjects = await _context.CurriculumSubjects.AsNoTracking()
            .Include(cs => cs.Subject)
            .Include(cs => cs.GradeLevel)
            .Include(cs => cs.CurriculumTrack)
            .Where(cs => cs.CurriculumTrack.SchoolId == null || cs.CurriculumTrack.SchoolId == currentUser.SchoolId)
            .ToListAsync();
        var subjectsByNameAndLevel = curriculumSubjects
            .GroupBy(cs => (Subject: NormalizeKey(cs.Subject.Name), Level: NormalizeKey(cs.GradeLevel?.Name ?? cs.LevelName)))
            .ToDictionary(g => g.Key, g => g.First());

        var studentIds = students.Values.Select(s => s.Id).ToList();
        var curriculumSubjectIds = curriculumSubjects.Select(cs => cs.Id).ToList();
        var existingCredits = await _context.StudentAcademicCredits.AsNoTracking()
            .Where(c => studentIds.Contains(c.StudentId) &&
                        curriculumSubjectIds.Contains(c.CurriculumSubjectId) &&
                        c.Status == "Valid")
            .Select(c => new { c.StudentId, c.CurriculumSubjectId })
            .ToListAsync();
        var existingPairs = existingCredits
            .Select(c => (c.StudentId, c.CurriculumSubjectId))
            .ToHashSet();
        var newPairs = new HashSet<(Guid StudentId, Guid CurriculumSubjectId)>();
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            try
            {
                if (!students.TryGetValue(NormalizeKey(row.Email), out var student))
                    throw new InvalidOperationException($"Estudiante no encontrado: {row.Email}");

                var subjectKey = (Subject: NormalizeKey(row.SubjectName), Level: NormalizeKey(row.LevelName));
                if (!subjectsByNameAndLevel.TryGetValue(subjectKey, out var curriculumSubject))
                    throw new InvalidOperationException($"Materia curricular no encontrada: {row.SubjectName} {row.LevelName}");

                var pair = (student.Id, curriculumSubject.Id);
                if (existingPairs.Contains(pair) || !newPairs.Add(pair))
                    continue;

                decimal? score = decimal.TryParse(row.ScoreText, out var parsed) ? parsed : null;
                _context.StudentAcademicCredits.Add(new StudentAcademicCredit
                {
                    Id = Guid.NewGuid(),
                    SchoolId = currentUser.SchoolId,
                    StudentId = student.Id,
                    CurriculumSubjectId = curriculumSubject.Id,
                    SubjectId = curriculumSubject.SubjectId,
                    GradeLevelId = curriculumSubject.GradeLevelId,
                    SourceType = "BulkEquivalence",
                    FinalScore = score,
                    ApprovedAt = now,
                    Status = "Valid",
                    Notes = $"Carga masiva CELOSAM desde {file.FileName}",
                    CreatedAt = now,
                    CreatedBy = currentUser.Id
                });
                success++;
            }
            catch (Exception ex)
            {
                errors.Add($"Fila {row.RowNumber}: {ex.Message}");
            }
        }

        await LogAsync(currentUser, file.FileName, processed, success, errors);
        await _context.SaveChangesAsync();
        return new CelosanBulkImportResult(errors.Count == 0, processed, success, errors.Count, errors);
    }

    private async Task LogAsync(User currentUser, string fileName, int processed, int success, IReadOnlyList<string> errors)
    {
        _context.CelosanBulkImportLogs.Add(new CelosanBulkImportLog
        {
            Id = Guid.NewGuid(),
            SchoolId = currentUser.SchoolId!.Value,
            ImportType = "ApprovedCredits",
            FileName = fileName,
            ProcessedRows = processed,
            SuccessRows = success,
            ErrorRows = errors.Count,
            ErrorSummary = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors.Take(20)),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUser.Id
        });
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            SchoolId = currentUser.SchoolId,
            UserId = currentUser.Id,
            UserName = $"{currentUser.Name} {currentUser.LastName}",
            UserRole = currentUser.Role,
            Action = "CelosanBulkImport.ApprovedCredits",
            Resource = "CelosanBulkImportLog",
            Details = $"Archivo: {fileName}; procesadas: {processed}; exitosas: {success}; errores: {errors.Count}",
            Timestamp = DateTime.UtcNow
        });
    }

    private static string Get(IReadOnlyList<string> columns, int index)
    {
        return index >= 0 && index < columns.Count ? columns[index].Trim() : string.Empty;
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static IEnumerable<string> SplitCsv(string line)
    {
        var current = new List<char>();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (ch == ',' && !quoted)
            {
                yield return new string(current.ToArray());
                current.Clear();
                continue;
            }
            current.Add(ch);
        }
        yield return new string(current.ToArray());
    }
}

public class CelosanReportService : ICelosanReportService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CelosanReportService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CelosanReportDashboardDto> BuildDashboardAsync(Guid? schoolId = null)
    {
        schoolId ??= (await _currentUserService.GetCurrentUserAsync())?.SchoolId;

        var prematriculated = await _context.Prematriculations.AsNoTracking()
            .Include(p => p.Student)
            .Where(p => !schoolId.HasValue || p.SchoolId == schoolId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new CelosanReportRow($"{p.Student.Name} {p.Student.LastName}", p.Status, p.PrematriculationCode))
            .ToListAsync();

        var demandData = await _context.StudentPrematriculationSubjectSelections.AsNoTracking()
            .Include(s => s.CurriculumSubject).ThenInclude(cs => cs.Subject)
            .Where(s => (!schoolId.HasValue || s.SchoolId == schoolId.Value) && s.Status != "Removed")
            .GroupBy(s => s.CurriculumSubject.Subject.Name)
            .Select(g => new { SubjectName = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();
        var demand = demandData
            .Select(g => new CelosanReportRow(g.SubjectName, g.Count.ToString(), "Prematriculados/seleccionados"))
            .ToList();

        var groups = await _context.SubjectAssignments.AsNoTracking()
            .Include(sa => sa.Group)
            .Include(sa => sa.Subject)
            .Include(sa => sa.TeacherAssignments).ThenInclude(ta => ta.Teacher)
            .Where(sa => !schoolId.HasValue || sa.SchoolId == null || sa.SchoolId == schoolId.Value)
            .ToListAsync();

        var groupAssignmentIds = groups.Select(g => g.Id).ToList();
        var activeCountsByAssignment = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => groupAssignmentIds.Contains(ssa.SubjectAssignmentId) && ssa.IsActive)
            .GroupBy(ssa => ssa.SubjectAssignmentId)
            .Select(g => new { SubjectAssignmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectAssignmentId, x => x.Count);

        var seats = new List<CelosanReportRow>();
        var fullGroups = new List<CelosanReportRow>();
        var studentsByTeacher = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in groups)
        {
            var active = activeCountsByAssignment.GetValueOrDefault(assignment.Id);
            var max = assignment.Group.MaxCapacity ?? 0;
            var available = max <= 0 ? 0 : Math.Max(max - active, 0);
            seats.Add(new CelosanReportRow($"{assignment.Subject.Name} - {assignment.Group.Name}", available.ToString(), $"Cupo maximo: {(max <= 0 ? "Sin definir" : max)}"));
            if (max > 0 && active >= max)
                fullGroups.Add(new CelosanReportRow($"{assignment.Subject.Name} - {assignment.Group.Name}", active.ToString(), "Grupo lleno"));

            foreach (var teacherAssignment in assignment.TeacherAssignments)
            {
                var teacherName = $"{teacherAssignment.Teacher.Name} {teacherAssignment.Teacher.LastName}";
                studentsByTeacher[teacherName] = studentsByTeacher.GetValueOrDefault(teacherName) + active;
            }
        }

        var expiredDocuments = await _context.StudentIdentityDocuments.AsNoTracking()
            .Include(d => d.Student)
            .Where(d => (!schoolId.HasValue || d.SchoolId == schoolId.Value) && (d.Status == "Expired" || (d.ExpirationDate.HasValue && d.ExpirationDate.Value.Date < DateTime.UtcNow.Date)))
            .Select(d => new CelosanReportRow($"{d.Student.Name} {d.Student.LastName}", d.Status, d.ExpirationDate.HasValue ? d.ExpirationDate.Value.ToString("yyyy-MM-dd") : "Sin vencimiento"))
            .ToListAsync();

        var withdrawnSubjects = await _context.StudentSubjectAssignments.AsNoTracking()
            .Include(ssa => ssa.Student)
            .Include(ssa => ssa.SubjectAssignment).ThenInclude(sa => sa.Subject)
            .Where(ssa => ssa.Status == "Withdrawn" && (!schoolId.HasValue || ssa.SchoolId == schoolId.Value))
            .Select(ssa => new CelosanReportRow($"{ssa.Student.Name} {ssa.Student.LastName}", ssa.SubjectAssignment.Subject.Name, "Retirado"))
            .ToListAsync();

        var progressRows = await BuildProgressRowsAsync(schoolId);

        var history = await _context.AuditLogs.AsNoTracking()
            .Where(a => !schoolId.HasValue || a.SchoolId == schoolId.Value)
            .Where(a => a.Action != null && (a.Action.Contains("Prematriculation") || a.Action.Contains("Celosan") || a.Action.Contains("IdentityDocument")))
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .Select(a => new CelosanReportRow(a.Action ?? "Accion", a.UserName ?? "Sistema", a.Details))
            .ToListAsync();

        return new CelosanReportDashboardDto(
            prematriculated,
            demand,
            seats.OrderBy(r => r.Label).ToList(),
            fullGroups.OrderBy(r => r.Label).ToList(),
            studentsByTeacher.Select(kvp => new CelosanReportRow(kvp.Key, kvp.Value.ToString(), "Estudiantes activos")).OrderBy(r => r.Label).ToList(),
            expiredDocuments,
            withdrawnSubjects,
            progressRows,
            history);
    }

    private async Task<IReadOnlyList<CelosanReportRow>> BuildProgressRowsAsync(Guid? schoolId)
    {
        var students = await _context.Users.AsNoTracking()
            .Where(u => (u.Role == "student" || u.Role == "estudiante" || u.Role == "alumno") && (!schoolId.HasValue || u.SchoolId == schoolId.Value))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.Name)
            .Take(200)
            .ToListAsync();
        var totalPlan = await _context.CurriculumSubjects.CountAsync(cs => cs.IsActive);
        var studentIds = students.Select(s => s.Id).ToList();
        var approvedCounts = await _context.StudentAcademicCredits.AsNoTracking()
            .Where(c => studentIds.Contains(c.StudentId) && c.Status == "Valid")
            .GroupBy(c => c.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);
        var rows = new List<CelosanReportRow>();
        foreach (var student in students)
        {
            var approved = approvedCounts.GetValueOrDefault(student.Id);
            var percent = totalPlan == 0 ? 0 : Math.Round((decimal)approved * 100m / totalPlan, 1);
            rows.Add(new CelosanReportRow($"{student.Name} {student.LastName}", $"{percent}%", $"Aprobadas: {approved}/{totalPlan}"));
        }
        return rows;
    }
}
