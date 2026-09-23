using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class StudentGradeImportService : IStudentGradeImportService
{
    public const int PreviewTtlMinutes = 20;
    public const string ImportType = "StudentGradeImport";
    public const string PreviewCacheKeyPrefix = "grade-import-preview:";
    public const string CreatedAuditAction = "ImportedTrimesterGradeCreated";
    public const string UpdatedAuditAction = "ImportedTrimesterGradeUpdated";
    public const string BatchAuditAction = "StudentGradeImportConfirmed";

    private static readonly ConcurrentDictionary<string, byte> ConsumedPreviewTokens = new();

    private readonly SchoolDbContext _context;
    private readonly IAcademicYearService _academicYearService;
    private readonly IOfficialGradeService _officialGradeService;
    private readonly IMemoryCache _cache;

    public StudentGradeImportService(
        SchoolDbContext context,
        IAcademicYearService academicYearService,
        IOfficialGradeService officialGradeService,
        IMemoryCache cache)
    {
        _context = context;
        _academicYearService = academicYearService;
        _officialGradeService = officialGradeService;
        _cache = cache;
    }

    public async Task<GradeImportPageDto> GetPageAsync(Guid? schoolId)
    {
        if (!schoolId.HasValue || schoolId.Value == Guid.Empty)
        {
            return new GradeImportPageDto
            {
                SchoolError = "Debe existir una escuela concreta en el usuario autenticado antes de analizar el archivo. No se acepta SchoolId del formulario ni del Excel."
            };
        }

        var school = await _context.Schools.AsNoTracking()
            .Where(s => s.Id == schoolId.Value)
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync();
        if (school == null)
        {
            return new GradeImportPageDto
            {
                SchoolError = "La escuela del usuario autenticado no es válida."
            };
        }

        var years = await _academicYearService.GetAllBySchoolAsync(school.Id);
        return new GradeImportPageDto
        {
            SchoolId = school.Id,
            SchoolName = school.Name,
            Years = years.Select(y => new GradeImportYearOptionDto
            {
                Id = y.Id,
                Name = y.Name
            }).ToList()
        };
    }

    public async Task<GradeImportAnalyzeResult> AnalyzeAsync(
        Guid userId,
        Guid schoolId,
        Guid academicYearId,
        IFormFile file)
    {
        if (userId == Guid.Empty)
            return FileError("No se pudo identificar al usuario autenticado.");
        if (schoolId == Guid.Empty)
            return FileError("Debe existir una escuela concreta en el contexto autorizado antes de analizar el archivo.");
        if (academicYearId == Guid.Empty)
            return FileError("Debe seleccionar un año académico.");
        if (file == null || file.Length == 0)
            return FileError("Debe seleccionar un archivo Excel .xlsx.");
        if (file.Length > StudentGradeImportExcelParser.MaxFileBytes)
            return FileError($"El archivo supera el tamaño máximo de {StudentGradeImportExcelParser.MaxFileBytes / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(file.FileName ?? string.Empty);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return FileError("Solo se aceptan archivos .xlsx.");

        var year = (await _academicYearService.GetAllBySchoolAsync(schoolId))
            .FirstOrDefault(y => y.Id == academicYearId);
        if (year == null)
            return FileError("El año académico no pertenece a la escuela autorizada.");

        GradeImportParseResult parsed;
        try
        {
            await using var stream = file.OpenReadStream();
            parsed = StudentGradeImportExcelParser.Parse(stream, file.FileName);
        }
        catch (GradeImportParseException ex)
        {
            return FileError(ex.Message);
        }
        catch (Exception)
        {
            return FileError("No se pudo leer el archivo Excel.");
        }

        var context = await LoadSchoolContextAsync(schoolId);
        var operations = new List<GradeImportResolvedOperation>();

        foreach (var row in parsed.Rows)
            operations.AddRange(await BuildRowOperationsAsync(row, schoolId, year, context));

        MarkFileDuplicates(operations);

        var preview = new GradeImportPreviewDto
        {
            FileName = Path.GetFileName(file.FileName),
            SheetName = parsed.SheetName,
            AcademicYearId = year.Id,
            AcademicYearName = year.Name,
            TotalRows = parsed.DataRowCount,
            TotalOperations = operations.Count,
            NewCount = operations.Count(o => o.Display.Status == GradeImportStatus.Nuevo),
            UpdateCount = operations.Count(o => o.Display.Status == GradeImportStatus.Actualizar),
            UnchangedCount = operations.Count(o => o.Display.Status == GradeImportStatus.SinCambios),
            ErrorCount = operations.Count(o => o.Display.Status == GradeImportStatus.Error),
            Operations = operations.Select(o => o.Display).ToList()
        };

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        preview.PreviewToken = token;
        _cache.Set(PreviewCacheKeyPrefix + token, new GradeImportPreviewCacheEntry
        {
            UserId = userId,
            SchoolId = schoolId,
            AcademicYearId = year.Id,
            FileName = preview.FileName,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(PreviewTtlMinutes),
            Preview = preview,
            ResolvedOperations = operations
        }, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(PreviewTtlMinutes),
            Size = 1
        });

        return new GradeImportAnalyzeResult { Preview = preview };
    }

    public async Task<GradeImportConfirmResult> ConfirmAsync(
        Guid userId,
        Guid schoolId,
        string previewToken,
        string? userName,
        string? userRole)
    {
        if (userId == Guid.Empty || schoolId == Guid.Empty)
            return TokenFail();
        if (string.IsNullOrWhiteSpace(previewToken))
            return TokenFail();

        var cacheKey = PreviewCacheKeyPrefix + previewToken.Trim();
        if (!_cache.TryGetValue(cacheKey, out GradeImportPreviewCacheEntry? entry) || entry == null)
            return TokenFail();

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _cache.Remove(cacheKey);
            return TokenFail();
        }

        if (entry.UserId != userId || entry.SchoolId != schoolId)
            return TokenFail();

        if (entry.Preview.ErrorCount > 0 ||
            entry.ResolvedOperations.Any(o => o.Display.Status == GradeImportStatus.Error))
        {
            return new GradeImportConfirmResult
            {
                HasPreviewErrors = true,
                ErrorMessage = GradeImportConfirmMessages.PreviewHasErrors
            };
        }

        var year = (await _academicYearService.GetAllBySchoolAsync(schoolId))
            .FirstOrDefault(y => y.Id == entry.AcademicYearId);
        if (year == null || year.Id != entry.Preview.AcademicYearId)
            return TokenFail();

        if (!ConsumedPreviewTokens.TryAdd(previewToken.Trim(), 0))
            return TokenFail();
        _cache.Remove(cacheKey);

        List<GradeImportResolvedOperation> revalidated;
        try
        {
            var context = await LoadSchoolContextAsync(schoolId);
            var sourceRows = ReconstructRows(entry.ResolvedOperations);
            revalidated = new List<GradeImportResolvedOperation>();
            foreach (var row in sourceRows)
                revalidated.AddRange(await BuildRowOperationsAsync(row, schoolId, year, context));
            MarkFileDuplicates(revalidated);
            ApplyCachedIdentityGuard(entry.ResolvedOperations, revalidated);
        }
        catch (Exception)
        {
            return RevalidationFail();
        }

        if (revalidated.Count == 0 ||
            revalidated.Any(o => o.Display.Status == GradeImportStatus.Error) ||
            revalidated.Any(o =>
                !o.StudentId.HasValue ||
                !o.StudentAssignmentId.HasValue ||
                !o.StudentSubjectAssignmentId.HasValue ||
                !o.AcademicYearId.HasValue ||
                !o.TrimesterId.HasValue ||
                !IsPersistableMark(o.NewScore, o.NewStatus) ||
                o.AcademicYearId != year.Id))
        {
            return RevalidationFail();
        }

        var newCount = revalidated.Count(o => o.Display.Status == GradeImportStatus.Nuevo);
        var updateCount = revalidated.Count(o => o.Display.Status == GradeImportStatus.Actualizar);
        var unchangedCount = revalidated.Count(o => o.Display.Status == GradeImportStatus.SinCambios);
        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();
        var displayName = string.IsNullOrWhiteSpace(userName) ? userId.ToString() : userName.Trim();

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var batch = new CelosanBulkImportLog
            {
                Id = batchId,
                SchoolId = schoolId,
                ImportType = ImportType,
                FileName = entry.FileName,
                ProcessedRows = revalidated.Count,
                SuccessRows = newCount + updateCount,
                ErrorRows = 0,
                ErrorSummary = "Completed",
                CreatedAt = now,
                CreatedBy = userId,
                AcademicYearId = year.Id,
                NewCount = newCount,
                UpdateCount = updateCount,
                UnchangedCount = unchangedCount
            };
            _context.CelosanBulkImportLogs.Add(batch);

            foreach (var op in revalidated.Where(o => o.Display.Status == GradeImportStatus.Nuevo))
            {
                _context.StudentImportedTrimesterGrades.Add(new StudentImportedTrimesterGrade
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    StudentId = op.StudentId!.Value,
                    StudentSubjectAssignmentId = op.StudentSubjectAssignmentId!.Value,
                    StudentAssignmentId = op.StudentAssignmentId!.Value,
                    AcademicYearId = year.Id,
                    TrimesterId = op.TrimesterId!.Value,
                    Score = op.NewScore,
                    Status = ImportedTrimesterGradeStatus.Normalize(op.NewStatus),
                    Source = StudentImportedTrimesterGradeSource.ExcelImport,
                    ImportBatchId = batchId,
                    CreatedBy = userId,
                    CreatedAt = now
                });
                _context.AuditLogs.Add(BuildGradeAudit(
                    CreatedAuditAction, schoolId, userId, displayName, userRole, batchId, op, null, null, op.NewScore, op.NewStatus));
            }

            foreach (var op in revalidated.Where(o => o.Display.Status == GradeImportStatus.Actualizar))
            {
                var existing = await _context.StudentImportedTrimesterGrades
                    .FirstOrDefaultAsync(g =>
                        g.SchoolId == schoolId &&
                        g.StudentSubjectAssignmentId == op.StudentSubjectAssignmentId &&
                        g.AcademicYearId == year.Id &&
                        g.TrimesterId == op.TrimesterId);
                if (existing == null)
                    throw new InvalidOperationException("imported-missing-on-update");
                if (existing.SchoolId != schoolId)
                    throw new InvalidOperationException("imported-tenant-mismatch");

                var previousScore = existing.Score;
                var previousStatus = existing.Status;
                existing.Score = op.NewScore;
                existing.Status = ImportedTrimesterGradeStatus.Normalize(op.NewStatus);
                existing.UpdatedBy = userId;
                existing.UpdatedAt = now;
                existing.ImportBatchId = batchId;
                _context.AuditLogs.Add(BuildGradeAudit(
                    UpdatedAuditAction, schoolId, userId, displayName, userRole, batchId, op, previousScore, previousStatus, op.NewScore, op.NewStatus));
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                UserId = userId,
                UserName = displayName,
                UserRole = userRole,
                Action = BatchAuditAction,
                Resource = "CelosanBulkImportLog",
                Details = $"Batch={batchId}; File={entry.FileName}; Year={year.Name}; New={newCount}; Update={updateCount}; Unchanged={unchangedCount}; Errors=0",
                Timestamp = now
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return new GradeImportConfirmResult
            {
                ConcurrentChange = true,
                ErrorMessage = GradeImportConfirmMessages.AcademicChanged
            };
        }
        catch (InvalidOperationException)
        {
            await tx.RollbackAsync();
            return RevalidationFail();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return new GradeImportConfirmResult
        {
            Success = true,
            Summary = new GradeImportConfirmSummaryDto
            {
                ImportBatchId = batchId,
                FileName = entry.FileName,
                AcademicYearName = year.Name,
                Processed = revalidated.Count,
                NewCount = newCount,
                UpdateCount = updateCount,
                UnchangedCount = unchangedCount,
                ErrorCount = 0,
                CompletedAt = now,
                UserName = displayName
            }
        };
    }

    private static GradeImportAnalyzeResult FileError(string message) =>
        new() { FileError = true, FileErrorMessage = message };

    private static GradeImportConfirmResult TokenFail() =>
        new() { TokenInvalid = true, ErrorMessage = GradeImportConfirmMessages.TokenInvalid };

    private static GradeImportConfirmResult RevalidationFail() =>
        new() { RevalidationFailed = true, ErrorMessage = GradeImportConfirmMessages.AcademicChanged };

    private async Task<SchoolImportContext> LoadSchoolContextAsync(Guid schoolId)
    {
        var grades = await _context.GradeLevels.AsNoTracking()
            .Where(g => g.SchoolId == null || g.SchoolId == schoolId)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();
        var groups = await _context.Groups.AsNoTracking()
            .Where(g => g.SchoolId == null || g.SchoolId == schoolId)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();
        var shifts = await _context.Shifts.AsNoTracking()
            .Where(s => s.SchoolId == null || s.SchoolId == schoolId)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();
        var trimesters = await _context.Trimesters.AsNoTracking()
            .Where(t => t.SchoolId == null || t.SchoolId == schoolId)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        return new SchoolImportContext
        {
            Grades = grades.Select(g => (g.Id, g.Name, EnrollmentTypeConstants.ParseGradeNumber(g.Name))).ToList(),
            Groups = groups.Select(g => (g.Id, g.Name)).ToList(),
            Shifts = shifts.Select(s => (s.Id, s.Name)).ToList(),
            Trimesters = trimesters.Select(t => (t.Id, StudentGradeImportExcelParser.NormalizeTrimester(t.Name))).ToList()
        };
    }

    private async Task<List<GradeImportResolvedOperation>> BuildRowOperationsAsync(
        GradeImportParsedRow row,
        Guid schoolId,
        AcademicYear year,
        SchoolImportContext ctx)
    {
        var displayName = $"{row.FirstName} {row.LastName}".Trim();
        var scores = row.Scores.Where(s =>
                s.HasFormulaWithoutValue ||
                s.ScoreError != null ||
                s.Score.HasValue ||
                !string.IsNullOrWhiteSpace(s.RawValue))
            .ToList();

        if (scores.Count == 0)
        {
            return new List<GradeImportResolvedOperation>
            {
                ErrorOp(row, displayName, string.Empty, null, "La fila no tiene notas trimestrales para importar.")
            };
        }

        var operations = new List<GradeImportResolvedOperation>();
        var identity = await ResolveIdentityAsync(row, schoolId);
        if (identity.Error != null)
        {
            foreach (var score in scores)
                operations.Add(ErrorOp(row, displayName, score.TrimesterCode, score.Score, identity.Error, score.RawValue));
            return operations;
        }

        var enrollment = await ResolveEnrollmentAsync(identity.Student!, row, year.Id, ctx);
        if (enrollment.Error != null)
        {
            foreach (var score in scores)
                operations.Add(ErrorOp(row, identity.StudentName, score.TrimesterCode, score.Score, enrollment.Error, score.RawValue, identity.Student!.Id));
            return operations;
        }

        var subject = await ResolveSubjectAsync(identity.Student!.Id, enrollment.Assignment!.Id, row.SubjectName);
        if (subject.Error != null)
        {
            foreach (var score in scores)
                operations.Add(ErrorOp(row, identity.StudentName, score.TrimesterCode, score.Score, subject.Error, score.RawValue, identity.Student.Id, enrollment.Assignment.Id));
            return operations;
        }

        foreach (var score in scores)
        {
            if (string.IsNullOrWhiteSpace(score.RawValue) && !score.HasFormulaWithoutValue && !score.Score.HasValue)
                continue;

            var tri = ctx.Trimesters
                .Where(t => t.Code == score.TrimesterCode)
                .Select(t => (Guid?)t.Id)
                .Distinct()
                .ToList();
            if (tri.Count == 0)
            {
                operations.Add(ErrorOp(row, identity.StudentName, score.TrimesterCode, score.Score,
                    "No se encontró el trimestre " + score.TrimesterCode + " en la escuela.",
                    score.RawValue, identity.Student.Id, enrollment.Assignment.Id, subject.SsaId));
                continue;
            }
            if (tri.Count > 1)
            {
                operations.Add(ErrorOp(row, identity.StudentName, score.TrimesterCode, score.Score,
                    "El trimestre " + score.TrimesterCode + " es ambiguo en la escuela.",
                    score.RawValue, identity.Student.Id, enrollment.Assignment.Id, subject.SsaId));
                continue;
            }

            if (score.HasFormulaWithoutValue || score.ScoreError != null)
            {
                operations.Add(ErrorOp(row, identity.StudentName, score.TrimesterCode, null,
                    score.ScoreError ?? "La nota no es válida.",
                    score.RawValue, identity.Student.Id, enrollment.Assignment.Id, subject.SsaId, year.Id, tri[0]));
                continue;
            }

            if (!IsPersistableMark(score.Score, score.Status))
            {
                operations.Add(ErrorOp(row, identity.StudentName, score.TrimesterCode, null,
                    "La nota no es válida.",
                    score.RawValue, identity.Student.Id, enrollment.Assignment.Id, subject.SsaId, year.Id, tri[0]));
                continue;
            }

            var imported = await _context.StudentImportedTrimesterGrades.AsNoTracking()
                .Where(g =>
                    g.SchoolId == schoolId &&
                    g.StudentSubjectAssignmentId == subject.SsaId &&
                    g.AcademicYearId == year.Id &&
                    g.TrimesterId == tri[0]!.Value)
                .Select(g => new { g.Score, g.Status })
                .FirstOrDefaultAsync();

            var activityRows = await (
                from sas in _context.StudentActivityScores.AsNoTracking()
                join a in _context.Activities.AsNoTracking() on sas.ActivityId equals a.Id
                where sas.StudentId == identity.Student.Id
                      && sas.StudentSubjectAssignmentId == subject.SsaId
                select new { a.Type, a.Trimester, sas.Score }
            ).ToListAsync();
            var activityPairs = activityRows
                .Where(x => StudentGradeImportExcelParser.NormalizeTrimester(x.Trimester) == score.TrimesterCode)
                .Select(x => ((string?)x.Type, x.Score))
                .ToList();

            var official = await _officialGradeService.GetTrimesterGradeAsync(
                subject.SsaId!.Value, year.Id, tri[0]!.Value, activityPairs);

            var newStatus = ImportedTrimesterGradeStatus.Normalize(score.Status);
            var status = imported == null
                ? GradeImportStatus.Nuevo
                : MarksEqual(imported.Score, imported.Status, score.Score, newStatus)
                    ? GradeImportStatus.SinCambios
                    : GradeImportStatus.Actualizar;

            var origin = official.IsImported
                ? GradeImportCurrentOrigin.Imported
                : official.Score.HasValue
                    ? GradeImportCurrentOrigin.Activities
                    : GradeImportCurrentOrigin.None;

            var currentDisplay = OfficialGradeMark.Display(
                imported?.Score ?? official.Score,
                imported?.Status ?? official.Status,
                imported != null || official.IsImported);
            var newDisplay = OfficialGradeMark.Display(score.Score, newStatus, true);
            var message = status switch
            {
                GradeImportStatus.Nuevo => "Se crearía una nota oficial importada.",
                GradeImportStatus.Actualizar => $"Reemplazaría {currentDisplay} por {newDisplay}.",
                GradeImportStatus.SinCambios => "La nota importada ya coincide.",
                _ => string.Empty
            };

            operations.Add(new GradeImportResolvedOperation
            {
                StudentId = identity.Student.Id,
                StudentAssignmentId = enrollment.Assignment.Id,
                StudentSubjectAssignmentId = subject.SsaId,
                AcademicYearId = year.Id,
                TrimesterId = tri[0],
                NewScore = score.Score,
                NewStatus = newStatus,
                Display = BaseDisplay(row, identity.StudentName, score.TrimesterCode, score.Score)
                .Tap(d =>
                {
                    d.Status = status;
                    d.Message = message;
                    d.CurrentImportedScore = imported?.Score;
                    d.CurrentOfficialScore = official.Score;
                    d.CurrentStatus = imported?.Status ?? official.Status;
                    d.CurrentDisplay = currentDisplay;
                    d.NewStatus = newStatus;
                    d.NewDisplay = newDisplay;
                    d.CurrentOrigin = origin;
                })
            });
        }

        if (operations.Count == 0)
        {
            operations.Add(ErrorOp(row, identity.StudentName, string.Empty, null,
                "La fila no tiene notas trimestrales para importar.",
                null, identity.Student.Id, enrollment.Assignment.Id, subject.SsaId));
        }

        return operations;
    }

    private async Task<(User? Student, string StudentName, string? Error)> ResolveIdentityAsync(
        GradeImportParsedRow row,
        Guid schoolId)
    {
        var document = row.DocumentId.Trim();
        var email = row.Email.Trim();
        if (string.IsNullOrWhiteSpace(document))
            return (null, DisplayName(row), "Documento vacío.");
        if (string.IsNullOrWhiteSpace(email))
            return (null, DisplayName(row), "Email vacío.");

        var byDocument = await _context.Users.AsNoTracking()
            .Where(u => u.SchoolId == schoolId && u.DocumentId != null && u.DocumentId.Trim() == document)
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email, u.DocumentId })
            .ToListAsync();

        var byEmail = await _context.Users.AsNoTracking()
            .Where(u => u.SchoolId == schoolId && u.Email != null && u.Email.ToLower() == email.ToLower())
            .Select(u => new { u.Id, u.Name, u.LastName, u.Email, u.DocumentId })
            .ToListAsync();

        if (byDocument.Count == 0 && byEmail.Count == 0)
            return (null, DisplayName(row), "No se encontró estudiante con ese documento ni email en esta escuela.");
        if (byDocument.Count == 0 && byEmail.Count > 0)
            return (null, DisplayName(row), "El email existe, pero el documento no corresponde a ese estudiante.");
        if (byDocument.Count > 1)
            return (null, DisplayName(row), "El documento es ambiguo dentro de la escuela.");
        if (byEmail.Count > 1)
            return (null, DisplayName(row), "El email es ambiguo dentro de la escuela.");

        var docUser = byDocument[0];
        if (byEmail.Count == 1 && byEmail[0].Id != docUser.Id)
            return (null, DisplayName(row), "ERROR CRÍTICO: el documento y el email corresponden a personas diferentes.");
        if (!string.Equals(docUser.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
            return (null, DisplayName(row), "El documento existe, pero el email no coincide.");

        var user = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == docUser.Id);
        return (user, $"{user.Name} {user.LastName}".Trim(), null);
    }

    private async Task<(StudentAssignment? Assignment, string? Error)> ResolveEnrollmentAsync(
        User student,
        GradeImportParsedRow row,
        Guid yearId,
        SchoolImportContext ctx)
    {
        var gradeNumber = StudentGradeImportExcelParser.NormalizeGradeNumber(row.GradeRaw);
        if (!gradeNumber.HasValue)
            return (null, "El nivel/grado del Excel no es válido.");

        var gradeIds = ctx.Grades.Where(g => g.Number == gradeNumber.Value).Select(g => g.Id).Distinct().ToList();
        if (gradeIds.Count == 0)
            return (null, "El nivel/grado no existe en la escuela.");
        if (gradeIds.Count > 1)
            return (null, "El nivel/grado es ambiguo en la escuela.");

        var groupKey = StudentGradeImportExcelParser.NormalizeKey(row.GroupRaw);
        if (string.IsNullOrWhiteSpace(groupKey))
            return (null, "El grupo está vacío.");
        var groupIds = ctx.Groups
            .Where(g => StudentGradeImportExcelParser.NormalizeKey(g.Name) == groupKey)
            .Select(g => g.Id)
            .Distinct()
            .ToList();
        if (groupIds.Count == 0)
            return (null, "El grupo no existe en la escuela.");
        if (groupIds.Count > 1)
            return (null, "El grupo es ambiguo en la escuela.");

        var shiftKey = StudentGradeImportExcelParser.NormalizeKey(row.ShiftRaw);
        if (string.IsNullOrWhiteSpace(shiftKey))
            return (null, "La jornada está vacía.");
        var shiftIds = ctx.Shifts
            .Where(s => StudentGradeImportExcelParser.NormalizeKey(s.Name) == shiftKey)
            .Select(s => s.Id)
            .Distinct()
            .ToList();
        if (shiftIds.Count == 0)
            return (null, "La jornada no existe en la escuela.");
        if (shiftIds.Count > 1)
            return (null, "La jornada es ambigua en la escuela.");

        var matches = await _context.StudentAssignments.AsNoTracking()
            .Where(sa =>
                sa.StudentId == student.Id &&
                sa.AcademicYearId == yearId &&
                sa.GradeId == gradeIds[0] &&
                sa.GroupId == groupIds[0])
            .ToListAsync();

        if (matches.Count == 0)
            return (null, "No hay una matrícula válida para ese año, grado, grupo y jornada.");

        var withShift = matches.Where(sa => sa.ShiftId == shiftIds[0]).ToList();
        if (withShift.Count == 1)
            return (withShift[0], null);
        if (withShift.Count > 1)
            return (null, "Hay más de una matrícula válida para ese año, grado, grupo y jornada.");

        var withoutShift = matches.Where(sa => sa.ShiftId == null).ToList();
        if (matches.Count == 1 && withoutShift.Count == 1)
            return (withoutShift[0], null);

        return (null, "El estudiante está matriculado en otra jornada o la matrícula no tiene jornada resoluble.");
    }

    private async Task<(Guid? SsaId, string? Error)> ResolveSubjectAsync(
        Guid studentId,
        Guid assignmentId,
        string subjectName)
    {
        var key = StudentGradeImportExcelParser.NormalizeKey(subjectName);
        if (string.IsNullOrWhiteSpace(key))
            return (null, "La asignatura está vacía.");

        var enrolled = await (
            from ssa in _context.StudentSubjectAssignments.AsNoTracking()
            join sa in _context.SubjectAssignments.AsNoTracking() on ssa.SubjectAssignmentId equals sa.Id
            join sub in _context.Subjects.AsNoTracking() on sa.SubjectId equals sub.Id
            where ssa.StudentId == studentId
                  && ssa.StudentAssignmentId == assignmentId
                  && ssa.IsActive
            select new { ssa.Id, SubjectName = sub.Name }
        ).ToListAsync();

        var hits = enrolled
            .Where(e => StudentGradeImportExcelParser.NormalizeKey(e.SubjectName) == key)
            .Select(e => e.Id)
            .Distinct()
            .ToList();

        if (hits.Count == 0)
            return (null, "El estudiante no tiene una inscripción activa en esa asignatura.");
        if (hits.Count > 1)
            return (null, "ASIGNATURA AMBIGUA. El estudiante tiene más de una inscripción activa con ese nombre.");

        return (hits[0], null);
    }

    private static void MarkFileDuplicates(List<GradeImportResolvedOperation> operations)
    {
        var groups = operations
            .Where(o => o.StudentSubjectAssignmentId.HasValue && o.AcademicYearId.HasValue && o.TrimesterId.HasValue)
            .GroupBy(o => (o.StudentSubjectAssignmentId, o.AcademicYearId, o.TrimesterId));

        foreach (var group in groups)
        {
            if (group.Count() < 2)
                continue;
            foreach (var op in group)
            {
                op.Display.Status = GradeImportStatus.Error;
                op.Display.Message = "Nota duplicada para estudiante/asignatura/trimestre dentro del archivo.";
            }
        }
    }

    private static GradeImportResolvedOperation ErrorOp(
        GradeImportParsedRow row,
        string studentName,
        string trimester,
        decimal? newScore,
        string message,
        string? rawValue = null,
        Guid? studentId = null,
        Guid? assignmentId = null,
        Guid? ssaId = null,
        Guid? yearId = null,
        Guid? trimesterId = null)
    {
        var display = BaseDisplay(row, studentName, trimester, newScore);
        display.Status = GradeImportStatus.Error;
        display.Message = message;
        if (!string.IsNullOrWhiteSpace(rawValue) && !newScore.HasValue)
            display.Message = message;
        return new GradeImportResolvedOperation
        {
            StudentId = studentId,
            StudentAssignmentId = assignmentId,
            StudentSubjectAssignmentId = ssaId,
            AcademicYearId = yearId,
            TrimesterId = trimesterId,
            NewScore = newScore,
            Display = display
        };
    }

    private static GradeImportOperationDto BaseDisplay(
        GradeImportParsedRow row,
        string studentName,
        string trimester,
        decimal? newScore)
    {
        return new GradeImportOperationDto
        {
            RowNumber = row.RowNumber,
            DocumentId = row.DocumentId.Trim(),
            Email = row.Email.Trim(),
            StudentName = string.IsNullOrWhiteSpace(studentName) ? DisplayName(row) : studentName,
            SubjectName = row.SubjectName.Trim(),
            GradeName = row.GradeRaw.Trim(),
            GroupName = row.GroupRaw.Trim(),
            ShiftName = row.ShiftRaw.Trim(),
            Trimester = trimester,
            NewScore = newScore,
            CurrentOrigin = GradeImportCurrentOrigin.None
        };
    }

    private static string DisplayName(GradeImportParsedRow row) =>
        $"{row.FirstName} {row.LastName}".Trim();

    private static List<GradeImportParsedRow> ReconstructRows(IEnumerable<GradeImportResolvedOperation> operations)
    {
        return operations
            .GroupBy(o => o.Display.RowNumber)
            .Select(group =>
            {
                var first = group.First().Display;
                var row = new GradeImportParsedRow
                {
                    RowNumber = first.RowNumber,
                    DocumentId = first.DocumentId,
                    Email = first.Email,
                    FirstName = first.StudentName,
                    SubjectName = first.SubjectName,
                    GradeRaw = first.GradeName,
                    GroupRaw = first.GroupName,
                    ShiftRaw = first.ShiftName
                };

                foreach (var op in group)
                {
                    var score = new GradeImportParsedScore
                    {
                        TrimesterCode = StudentGradeImportExcelParser.NormalizeTrimester(op.Display.Trimester)
                    };
                    var markStatus = ImportedTrimesterGradeStatus.Normalize(op.NewStatus);
                    score.Status = markStatus;
                    if (ImportedTrimesterGradeStatus.IsSpecial(markStatus))
                    {
                        score.Score = null;
                        score.RawValue = OfficialGradeMark.Display(null, markStatus, true);
                    }
                    else if (!op.NewScore.HasValue)
                    {
                        score.ScoreError = "La nota no es válida.";
                        score.RawValue = op.Display.Message;
                    }
                    else if (!StudentGradeImportExcelParser.TryParseScore(op.NewScore.Value, out var parsed, out var error))
                    {
                        score.ScoreError = error;
                        score.RawValue = op.NewScore.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        score.Score = parsed;
                        score.RawValue = parsed.ToString("0.0", CultureInfo.InvariantCulture);
                    }

                    row.Scores.Add(score);
                }

                return row;
            })
            .ToList();
    }

    private static void ApplyCachedIdentityGuard(
        List<GradeImportResolvedOperation> original,
        List<GradeImportResolvedOperation> revalidated)
    {
        foreach (var current in revalidated)
        {
            var previous = original.FirstOrDefault(o =>
                o.Display.RowNumber == current.Display.RowNumber &&
                StudentGradeImportExcelParser.NormalizeTrimester(o.Display.Trimester) ==
                StudentGradeImportExcelParser.NormalizeTrimester(current.Display.Trimester));
            if (previous == null)
            {
                current.Display.Status = GradeImportStatus.Error;
                current.Display.Message = GradeImportConfirmMessages.AcademicChanged;
                continue;
            }

            if (previous.StudentId.HasValue && current.StudentId != previous.StudentId ||
                previous.StudentAssignmentId.HasValue && current.StudentAssignmentId != previous.StudentAssignmentId ||
                previous.StudentSubjectAssignmentId.HasValue && current.StudentSubjectAssignmentId != previous.StudentSubjectAssignmentId ||
                previous.AcademicYearId.HasValue && current.AcademicYearId != previous.AcademicYearId ||
                previous.TrimesterId.HasValue && current.TrimesterId != previous.TrimesterId)
            {
                current.Display.Status = GradeImportStatus.Error;
                current.Display.Message = GradeImportConfirmMessages.AcademicChanged;
            }
        }
    }

    private static bool IsPersistableMark(decimal? score, string? status)
    {
        var normalized = ImportedTrimesterGradeStatus.Normalize(status);
        if (ImportedTrimesterGradeStatus.IsSpecial(normalized))
            return !score.HasValue;
        return score.HasValue;
    }

    private static bool MarksEqual(decimal? leftScore, string? leftStatus, decimal? rightScore, string? rightStatus)
    {
        if (ImportedTrimesterGradeStatus.Normalize(leftStatus) != ImportedTrimesterGradeStatus.Normalize(rightStatus))
            return false;
        if (ImportedTrimesterGradeStatus.IsSpecial(leftStatus))
            return true;
        return leftScore == rightScore;
    }

    private static AuditLog BuildGradeAudit(
        string action,
        Guid schoolId,
        Guid userId,
        string userName,
        string? userRole,
        Guid batchId,
        GradeImportResolvedOperation op,
        decimal? previousScore,
        string? previousStatus,
        decimal? nextScore,
        string? nextStatus)
    {
        var previousText = OfficialGradeMark.Display(previousScore, previousStatus, previousStatus != null);
        var nextText = OfficialGradeMark.Display(nextScore, nextStatus, true);
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = userId,
            UserName = userName,
            UserRole = userRole,
            Action = action,
            Resource = "StudentImportedTrimesterGrade",
            Details =
                $"Batch={batchId}; StudentId={op.StudentId}; Document={op.Display.DocumentId}; " +
                $"SSA={op.StudentSubjectAssignmentId}; Subject={op.Display.SubjectName}; " +
                $"Year={op.AcademicYearId}; Trimester={op.Display.Trimester}; " +
                $"Previous={previousText}; PreviousStatus={ImportedTrimesterGradeStatus.Normalize(previousStatus)}; " +
                $"New={nextText}; NewStatus={ImportedTrimesterGradeStatus.Normalize(nextStatus)}",
            Timestamp = DateTime.UtcNow
        };
    }

    private sealed class SchoolImportContext
    {
        public List<(Guid Id, string Name, int? Number)> Grades { get; set; } = new();
        public List<(Guid Id, string Name)> Groups { get; set; } = new();
        public List<(Guid Id, string Name)> Shifts { get; set; } = new();
        public List<(Guid Id, string Code)> Trimesters { get; set; } = new();
    }
}

internal static class GradeImportDisplayExtensions
{
    public static GradeImportOperationDto Tap(this GradeImportOperationDto dto, Action<GradeImportOperationDto> action)
    {
        action(dto);
        return dto;
    }
}
