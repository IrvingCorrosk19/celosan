using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class CelosamPrematriculationModuleService : ICelosamPrematriculationModuleService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAcademicPrerequisiteService _prerequisiteService;
    private readonly IModularEnrollmentService _modularEnrollmentService;

    public CelosamPrematriculationModuleService(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        IAcademicPrerequisiteService prerequisiteService,
        IModularEnrollmentService modularEnrollmentService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _prerequisiteService = prerequisiteService;
        _modularEnrollmentService = modularEnrollmentService;
    }

    private async Task AddAuditAsync(Guid schoolId, Guid? userId, string action, string resource, string details)
    {
        var user = userId.HasValue
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value)
            : null;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = userId,
            UserName = user == null ? null : $"{user.Name} {user.LastName}".Trim(),
            UserRole = user?.Role,
            Action = action,
            Resource = resource,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task<CelosamPrematriculationDashboardDto?> GetDashboardAsync(Guid prematriculationId)
    {
        var prematriculation = await _context.Prematriculations
            .Include(p => p.Student)
            .Include(p => p.PrematriculationPeriod)
            .FirstOrDefaultAsync(p => p.Id == prematriculationId);
        if (prematriculation == null)
            return null;

        var period = prematriculation.PrematriculationPeriod;
        var track = await ResolveTrackAsync(prematriculation.SchoolId, period.AcademicYearId);
        if (track == null)
        {
            return new CelosamPrematriculationDashboardDto(
                prematriculation,
                period,
                Array.Empty<CelosamAvailableSubjectDto>(),
                await GetSelectedSubjectsAsync(prematriculation.Id),
                new CelosamAcademicProgressDto(0, 0, 0, 0, 0));
        }

        var subjects = await _context.CurriculumSubjects
            .Include(cs => cs.Subject)
            .Include(cs => cs.GradeLevel)
            .Where(cs => cs.CurriculumTrackId == track.Id && cs.IsActive)
            .OrderBy(cs => cs.ModuleOrder)
            .ThenBy(cs => cs.Subject.Name)
            .ToListAsync();

        var selectedIds = await _context.StudentPrematriculationSubjectSelections
            .Where(s => s.PrematriculationId == prematriculation.Id && s.Status != "Removed")
            .Select(s => s.CurriculumSubjectId)
            .ToHashSetAsync();

        var subjectIds = subjects.Select(s => s.Id).ToList();
        var validCreditIds = await _context.StudentAcademicCredits.AsNoTracking()
            .Where(c => c.StudentId == prematriculation.StudentId &&
                        subjectIds.Contains(c.CurriculumSubjectId) &&
                        c.Status == "Valid")
            .Select(c => c.CurriculumSubjectId)
            .ToHashSetAsync();
        var failedIds = await _context.SubjectPromotionRecords.AsNoTracking()
            .Where(r => r.StudentId == prematriculation.StudentId &&
                        r.CurriculumSubjectId.HasValue &&
                        subjectIds.Contains(r.CurriculumSubjectId.Value) &&
                        r.Outcome == "Failed")
            .Select(r => r.CurriculumSubjectId!.Value)
            .ToHashSetAsync();
        var withdrawnIds = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => ssa.StudentId == prematriculation.StudentId &&
                          ssa.CurriculumSubjectId.HasValue &&
                          subjectIds.Contains(ssa.CurriculumSubjectId.Value) &&
                          ssa.Status == "Withdrawn")
            .Select(ssa => ssa.CurriculumSubjectId!.Value)
            .ToHashSetAsync();
        var availableGroupsBySubject = await CountAvailableGroupsBySubjectAsync(prematriculation.SchoolId, period, subjects);

        var available = new List<CelosamAvailableSubjectDto>();
        var approved = 0;
        var failedOrWithdrawn = 0;

        foreach (var subject in subjects)
        {
            var validCredit = validCreditIds.Contains(subject.Id);
            var failed = failedIds.Contains(subject.Id);
            var withdrawn = withdrawnIds.Contains(subject.Id);

            if (validCredit)
                approved++;
            if (failed || withdrawn)
                failedOrWithdrawn++;

            var validation = await _prerequisiteService.ValidateAsync(prematriculation.StudentId, subject.Id);
            var availableGroups = availableGroupsBySubject.GetValueOrDefault(subject.Id);
            var isSelected = selectedIds.Contains(subject.Id);
            var canSelect = !validCredit && !isSelected && validation.CanEnroll && availableGroups > 0;
            var message = validCredit
                ? "Ya aprobada"
                : isSelected
                    ? "Ya seleccionada"
                    : validation.CanEnroll
                        ? availableGroups > 0 ? "Disponible" : "Sin cupo/horario publicado"
                        : validation.Message;

            available.Add(new CelosamAvailableSubjectDto(
                subject.Id,
                subject.Subject.Name,
                subject.GradeLevel?.Name ?? subject.LevelName,
                subject.ModuleOrder,
                subject.Credits,
                validCredit,
                failed || withdrawn,
                canSelect,
                validCredit ? "Approved" : failed || withdrawn ? "PendingRepeat" : canSelect ? "Available" : "Blocked",
                message,
                availableGroups));
        }

        var total = subjects.Count;
        var progress = new CelosamAcademicProgressDto(
            total,
            approved,
            Math.Max(total - approved, 0),
            failedOrWithdrawn,
            total == 0 ? 0 : Math.Round((decimal)approved * 100m / total, 1));

        return new CelosamPrematriculationDashboardDto(
            prematriculation,
            period,
            available,
            await GetSelectedSubjectsAsync(prematriculation.Id),
            progress);
    }

    public async Task<(bool Success, string Message)> SelectSubjectAsync(Guid prematriculationId, Guid curriculumSubjectId)
    {
        var prematriculation = await _context.Prematriculations
            .Include(p => p.PrematriculationPeriod)
            .FirstOrDefaultAsync(p => p.Id == prematriculationId);
        if (prematriculation == null)
            return (false, "Prematricula no encontrada.");

        if (!IsEditable(prematriculation.Status))
            return (false, "La prematricula ya fue finalizada o no esta abierta para cambios.");

        var period = prematriculation.PrematriculationPeriod;
        if (!IsPeriodOpen(period))
            return (false, "El periodo de prematricula no esta abierto.");

        var currentCount = await _context.StudentPrematriculationSubjectSelections.CountAsync(s =>
            s.PrematriculationId == prematriculationId &&
            s.Status != "Removed");
        if (period.MaxSubjectsAllowed.HasValue && currentCount >= period.MaxSubjectsAllowed.Value)
            return (false, $"El periodo permite maximo {period.MaxSubjectsAllowed.Value} materias.");

        var subject = await _context.CurriculumSubjects
            .Include(cs => cs.Subject)
            .FirstOrDefaultAsync(cs => cs.Id == curriculumSubjectId && cs.IsActive);
        if (subject == null)
            return (false, "Materia curricular no encontrada.");

        var alreadyApproved = await _context.StudentAcademicCredits.AnyAsync(c =>
            c.StudentId == prematriculation.StudentId &&
            c.CurriculumSubjectId == curriculumSubjectId &&
            c.Status == "Valid");
        if (alreadyApproved)
            return (false, "La materia ya esta aprobada o convalidada.");

        var validation = await _prerequisiteService.ValidateAsync(prematriculation.StudentId, curriculumSubjectId);
        if (!validation.CanEnroll)
            return (false, validation.Message);

        var existing = await _context.StudentPrematriculationSubjectSelections.FirstOrDefaultAsync(s =>
            s.PrematriculationId == prematriculationId &&
            s.CurriculumSubjectId == curriculumSubjectId);
        if (existing != null)
        {
            if (existing.Status != "Removed")
                return (false, "La materia ya esta seleccionada.");

            existing.Status = "Draft";
            existing.ValidationStatus = "Validated";
            existing.ValidationMessage = null;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = await _currentUserService.GetCurrentUserIdAsync();
        }
        else
        {
            _context.StudentPrematriculationSubjectSelections.Add(new StudentPrematriculationSubjectSelection
            {
                Id = Guid.NewGuid(),
                SchoolId = prematriculation.SchoolId,
                PrematriculationId = prematriculation.Id,
                PrematriculationPeriodId = prematriculation.PrematriculationPeriodId,
                StudentId = prematriculation.StudentId,
                CurriculumSubjectId = curriculumSubjectId,
                Status = "Draft",
                ValidationStatus = "Validated",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = await _currentUserService.GetCurrentUserIdAsync()
            });
        }

        var userId = await _currentUserService.GetCurrentUserIdAsync();
        await AddAuditAsync(
            prematriculation.SchoolId,
            userId,
            "Prematriculation.SubjectSelected",
            "StudentPrematriculationSubjectSelection",
            $"Prematrícula {prematriculation.Id}: materia {subject.Subject.Name} seleccionada.");

        await _context.SaveChangesAsync();
        return (true, $"{subject.Subject.Name} agregada a la prematricula.");
    }

    public async Task<(bool Success, string Message)> RemoveSubjectAsync(Guid selectionId)
    {
        var selection = await _context.StudentPrematriculationSubjectSelections
            .Include(s => s.Prematriculation)
            .FirstOrDefaultAsync(s => s.Id == selectionId);
        if (selection == null)
            return (false, "Seleccion no encontrada.");

        if (!IsEditable(selection.Prematriculation.Status))
            return (false, "La prematricula no esta abierta para modificaciones.");

        selection.Status = "Removed";
        selection.UpdatedAt = DateTime.UtcNow;
        selection.UpdatedBy = await _currentUserService.GetCurrentUserIdAsync();

        await AddAuditAsync(
            selection.Prematriculation.SchoolId,
            selection.UpdatedBy,
            "Prematriculation.SubjectRemoved",
            "StudentPrematriculationSubjectSelection",
            $"Prematrícula {selection.PrematriculationId}: selección {selection.Id} removida.");

        await _context.SaveChangesAsync();
        return (true, "Materia removida de la prematricula.");
    }

    public async Task<CelosamFinalizeResult> FinalizeAsync(Guid prematriculationId)
    {
        var prematriculation = await _context.Prematriculations
            .Include(p => p.PrematriculationPeriod)
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == prematriculationId);
        if (prematriculation == null)
            return new CelosamFinalizeResult(false, "Prematricula no encontrada.", null);

        if (!IsEditable(prematriculation.Status))
            return new CelosamFinalizeResult(false, "La prematricula ya fue finalizada.", null);

        var period = prematriculation.PrematriculationPeriod;
        if (!IsPeriodOpen(period))
            return new CelosamFinalizeResult(false, "El periodo de prematricula esta cerrado.", null);

        var selections = await _context.StudentPrematriculationSubjectSelections
            .Include(s => s.CurriculumSubject)
            .Where(s => s.PrematriculationId == prematriculationId && s.Status != "Removed")
            .ToListAsync();
        if (selections.Count == 0)
            return new CelosamFinalizeResult(false, "Debe seleccionar al menos una materia.", null);
        if (period.MaxSubjectsAllowed.HasValue && selections.Count > period.MaxSubjectsAllowed.Value)
            return new CelosamFinalizeResult(false, $"Supera el maximo de {period.MaxSubjectsAllowed.Value} materias.", null);

        var assignedSchedules = new List<ScheduleEntry>();
        foreach (var selection in selections)
        {
            var assignment = await ResolveBestSubjectAssignmentAsync(prematriculation.SchoolId, period, selection.CurriculumSubject, assignedSchedules);
            if (assignment == null)
            {
                selection.ValidationStatus = "Blocked";
                selection.ValidationMessage = "Sin grupo disponible o sin horario compatible.";
                return new CelosamFinalizeResult(false, $"No hay grupo disponible para {selection.CurriculumSubject.Subject.Name}.", null);
            }

            selection.SubjectAssignmentId = assignment.Id;
            selection.GroupId = assignment.GroupId;
            selection.TeacherAssignmentId = assignment.TeacherAssignments.FirstOrDefault()?.Id;
            selection.Status = "Finalized";
            selection.ValidationStatus = "Validated";
            selection.ValidationMessage = null;
            selection.UpdatedAt = DateTime.UtcNow;
            selection.UpdatedBy = await _currentUserService.GetCurrentUserIdAsync();
            assignedSchedules.AddRange(await GetScheduleEntriesAsync(assignment.Id, period.AcademicYearId));
        }

        var trimesterId = period.TrimesterId ?? prematriculation.TargetTrimesterId;
        if (!trimesterId.HasValue)
            return new CelosamFinalizeResult(false, "El periodo no tiene trimestre academico configurado.", null);

        foreach (var selection in selections)
        {
            if (!selection.SubjectAssignmentId.HasValue)
                return new CelosamFinalizeResult(false, "Hay materias sin grupo asignado.", null);

            var result = await _modularEnrollmentService.EnrollSubjectAsync(
                new ModularSubjectEnrollmentRequest(
                    prematriculation.StudentId,
                    selection.SubjectAssignmentId.Value,
                    trimesterId,
                    prematriculation.EntryType,
                    false));
            if (result.Handled && !result.Success)
                return new CelosamFinalizeResult(false, result.Message, null);
        }

        prematriculation.Status = "Finalizada";
        prematriculation.UpdatedAt = DateTime.UtcNow;

        var receipt = await CreateReceiptAsync(prematriculation);
        await AddAuditAsync(
            prematriculation.SchoolId,
            await _currentUserService.GetCurrentUserIdAsync(),
            "Prematriculation.Finalized",
            "Prematriculation",
            $"Prematrícula {prematriculation.Id} finalizada con {selections.Count} materia(s). Comprobante {receipt.Consecutive}.");
        await _context.SaveChangesAsync();
        return new CelosamFinalizeResult(true, "Prematricula finalizada correctamente.", receipt.Id);
    }

    public async Task<(bool Success, string Message)> ReopenAsync(Guid prematriculationId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Debe indicar el motivo de reapertura.");

        var prematriculation = await _context.Prematriculations.FirstOrDefaultAsync(p => p.Id == prematriculationId);
        if (prematriculation == null)
            return (false, "Prematricula no encontrada.");

        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue)
            return (false, "No se pudo resolver el usuario actual.");

        prematriculation.Status = "Reabierta";
        prematriculation.UpdatedAt = DateTime.UtcNow;
        _context.PrematriculationReopenAuthorizations.Add(new PrematriculationReopenAuthorization
        {
            Id = Guid.NewGuid(),
            SchoolId = prematriculation.SchoolId,
            PrematriculationId = prematriculation.Id,
            PrematriculationPeriodId = prematriculation.PrematriculationPeriodId,
            StudentId = prematriculation.StudentId,
            Reason = reason.Trim(),
            AuthorizedAt = DateTime.UtcNow,
            AuthorizedBy = currentUserId.Value
        });

        await AddAuditAsync(
            prematriculation.SchoolId,
            currentUserId,
            "Prematriculation.Reopened",
            "Prematriculation",
            $"Prematrícula {prematriculation.Id} reabierta. Motivo: {reason.Trim()}");

        await _context.SaveChangesAsync();
        return (true, "Prematricula reabierta para modificacion.");
    }

    public async Task<byte[]?> GenerateReceiptPdfAsync(Guid receiptId)
    {
        var receipt = await _context.PrematriculationReceipts
            .Include(r => r.School)
            .Include(r => r.Student)
            .Include(r => r.PrematriculationPeriod)
            .FirstOrDefaultAsync(r => r.Id == receiptId);
        if (receipt == null)
            return null;

        var selections = await GetSelectedSubjectsAsync(receipt.PrematriculationId);
        var identityDocument = await _context.StudentIdentityDocuments.AsNoTracking()
            .Where(d => d.StudentId == receipt.StudentId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                page.Header().Column(c =>
                {
                    c.Item().Text(receipt.School.Name).FontSize(18).Bold().FontColor("#1f4e79");
                    c.Item().Text("Comprobante de prematricula CELOSAM").FontSize(14).SemiBold();
                    c.Item().Text($"{receipt.Consecutive} | Generado: {receipt.GeneratedAt:yyyy-MM-dd HH:mm}");
                    c.Item().LineHorizontal(1).LineColor("#1f4e79");
                });
                page.Content().PaddingTop(12).Column(c =>
                {
                    c.Item().Text($"Estudiante: {receipt.Student.Name} {receipt.Student.LastName}").Bold();
                    c.Item().Text($"Cedula/Documento: {receipt.Student.DocumentId ?? "No registrada"}");
                    c.Item().Text($"Periodo: {receipt.PrematriculationPeriod.Name ?? "Prematricula"}");
                    c.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn(1.3f);
                            cols.RelativeColumn(1.3f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background("#1f4e79").Padding(5).Text("Materia").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1f4e79").Padding(5).Text("Nivel").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1f4e79").Padding(5).Text("Grupo").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1f4e79").Padding(5).Text("Docente").FontColor(Colors.White).Bold();
                            h.Cell().Background("#1f4e79").Padding(5).Text("Horario").FontColor(Colors.White).Bold();
                        });
                        foreach (var item in selections)
                        {
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(item.SubjectName);
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(item.GradeName);
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(item.GroupName ?? "Por asignar");
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(item.TeacherName ?? "Por asignar docente");
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(item.ScheduleText);
                        }
                    });
                    c.Item().PaddingTop(24).Text("Espacio para sello de la escuela").Italic();
                    c.Item().Height(70).Border(1).BorderColor(Colors.Grey.Lighten1);
                    var documentText = identityDocument == null
                        ? "Documento de identidad: no hay archivo registrado."
                        : $"Documento de identidad: {identityDocument.DocumentNumber ?? receipt.Student.DocumentId ?? "Sin numero"} | Estado: {identityDocument.Status} | Vence: {(identityDocument.ExpirationDate.HasValue ? identityDocument.ExpirationDate.Value.ToString("yyyy-MM-dd") : "Sin fecha")} | Archivo: {identityDocument.FileUrl}";
                    c.Item().PaddingTop(10).Text(documentText).FontSize(9);
                });
            });
        }).GeneratePdf();
    }

    private async Task<CurriculumTrack?> ResolveTrackAsync(Guid schoolId, Guid? academicYearId)
    {
        var query = _context.CurriculumTracks
            .Where(t => t.IsActive && (t.SchoolId == null || t.SchoolId == schoolId));
        if (academicYearId.HasValue)
            query = query.Where(t => t.AcademicYearId == null || t.AcademicYearId == academicYearId);

        return await query
            .OrderByDescending(t => t.SchoolId != null)
            .ThenByDescending(t => t.AcademicYearId != null)
            .ThenByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private static bool IsPeriodOpen(PrematriculationPeriod period)
    {
        var now = DateTime.UtcNow;
        return period.IsActive && period.StartDate <= now && period.EndDate >= now;
    }

    private static bool IsEditable(string? status)
    {
        return status is null ||
               status.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Borrador", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Reabierta", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<Guid, int>> CountAvailableGroupsBySubjectAsync(
        Guid schoolId,
        PrematriculationPeriod period,
        IReadOnlyList<CurriculumSubject> subjects)
    {
        if (subjects.Count == 0)
            return new Dictionary<Guid, int>();

        var subjectIds = subjects.Select(s => s.SubjectId).Distinct().ToList();
        var candidates = await _context.SubjectAssignments
            .Include(sa => sa.Group)
                .ThenInclude(g => g.ShiftNavigation)
            .AsNoTracking()
            .Where(sa => subjectIds.Contains(sa.SubjectId) &&
                         (sa.SchoolId == null || sa.SchoolId == schoolId) &&
                         (sa.Status == null || sa.Status != "Closed") &&
                         (sa.Group.Shift == "Noche" ||
                          (sa.Group.ShiftNavigation != null && sa.Group.ShiftNavigation.Name == "Noche")))
            .ToListAsync();

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var scheduledAssignmentIds = await _context.ScheduleEntries.AsNoTracking()
            .Where(se => candidateIds.Contains(se.TeacherAssignment.SubjectAssignmentId) &&
                         (!period.AcademicYearId.HasValue || se.AcademicYearId == period.AcademicYearId.Value))
            .Select(se => se.TeacherAssignment.SubjectAssignmentId)
            .Distinct()
            .ToHashSetAsync();

        var activeCounts = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => candidateIds.Contains(ssa.SubjectAssignmentId) && ssa.IsActive)
            .GroupBy(ssa => ssa.SubjectAssignmentId)
            .Select(g => new { SubjectAssignmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectAssignmentId, x => x.Count);
        var finalizedCounts = await _context.StudentPrematriculationSubjectSelections.AsNoTracking()
            .Where(s => s.SubjectAssignmentId.HasValue &&
                        candidateIds.Contains(s.SubjectAssignmentId.Value) &&
                        s.Status == "Finalized")
            .GroupBy(s => s.SubjectAssignmentId!.Value)
            .Select(g => new { SubjectAssignmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectAssignmentId, x => x.Count);

        var result = new Dictionary<Guid, int>();
        foreach (var subject in subjects)
        {
            var count = candidates.Count(assignment =>
                assignment.SubjectId == subject.SubjectId &&
                (!subject.GradeLevelId.HasValue || assignment.GradeLevelId == subject.GradeLevelId.Value) &&
                IsGroupCompatibleWithGrade(assignment.Group, subject.GradeLevel?.Name ?? subject.LevelName) &&
                scheduledAssignmentIds.Contains(assignment.Id) &&
                activeCounts.GetValueOrDefault(assignment.Id) + finalizedCounts.GetValueOrDefault(assignment.Id) <
                (assignment.Group.MaxCapacity ?? period.MaxCapacityPerGroup));
            result[subject.Id] = count;
        }

        return result;
    }

    private async Task<IReadOnlyList<SubjectAssignment>> GetCandidateAssignmentsAsync(Guid schoolId, CurriculumSubject subject)
    {
        var gradeLevelId = subject.GradeLevelId;
        var assignments = await _context.SubjectAssignments
            .Include(sa => sa.Group)
                .ThenInclude(g => g.ShiftNavigation)
            .Include(sa => sa.TeacherAssignments)
                .ThenInclude(ta => ta.Teacher)
            .Where(sa => sa.SubjectId == subject.SubjectId &&
                         (sa.SchoolId == null || sa.SchoolId == schoolId) &&
                         (!gradeLevelId.HasValue || sa.GradeLevelId == gradeLevelId.Value) &&
                         (sa.Status == null || sa.Status != "Closed") &&
                         (sa.Group.Shift == "Noche" ||
                          (sa.Group.ShiftNavigation != null && sa.Group.ShiftNavigation.Name == "Noche")))
            .ToListAsync();

        return assignments
            .Where(a => IsGroupCompatibleWithGrade(a.Group, subject.GradeLevel?.Name ?? subject.LevelName))
            .ToList();
    }

    private static bool IsGroupCompatibleWithGrade(Group? group, string? gradeName)
    {
        if (group == null)
            return false;

        var gradeNumber = ExtractGradeNumber(gradeName);
        if (gradeNumber == null)
            return true;

        if (!string.IsNullOrWhiteSpace(group.Grade))
            return ExtractGradeNumber(group.Grade) == gradeNumber;

        return group.Name.StartsWith($"{gradeNumber}-", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ExtractGradeNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(value, @"\d+");
        return match.Success && int.TryParse(match.Value, out var number) ? number : null;
    }

    private async Task<SubjectAssignment?> ResolveBestSubjectAssignmentAsync(
        Guid schoolId,
        PrematriculationPeriod period,
        CurriculumSubject subject,
        IReadOnlyList<ScheduleEntry> alreadyAssignedSchedules)
    {
        var candidates = await GetCandidateAssignmentsAsync(schoolId, subject);
        var ranked = new List<(SubjectAssignment Assignment, int ActiveCount)>();
        foreach (var assignment in candidates)
        {
            if (!await HasCapacityAsync(assignment, period))
                continue;

            var schedule = await GetScheduleEntriesAsync(assignment.Id, period.AcademicYearId);
            if (HasScheduleConflict(alreadyAssignedSchedules, schedule))
                continue;

            var activeCount = await _context.StudentSubjectAssignments.CountAsync(ssa =>
                ssa.SubjectAssignmentId == assignment.Id && ssa.IsActive);
            ranked.Add((assignment, activeCount));
        }

        return ranked.OrderBy(x => x.ActiveCount).ThenBy(x => x.Assignment.Group.Name).FirstOrDefault().Assignment;
    }

    private async Task<bool> HasCapacityAsync(SubjectAssignment assignment, PrematriculationPeriod period)
    {
        var activeCount = await _context.StudentSubjectAssignments.CountAsync(ssa =>
            ssa.SubjectAssignmentId == assignment.Id && ssa.IsActive);
        var draftCount = await _context.StudentPrematriculationSubjectSelections.CountAsync(s =>
            s.SubjectAssignmentId == assignment.Id && s.Status == "Finalized");
        var limit = assignment.Group.MaxCapacity ?? period.MaxCapacityPerGroup;
        return activeCount + draftCount < limit;
    }

    private async Task<List<ScheduleEntry>> GetScheduleEntriesAsync(Guid subjectAssignmentId, Guid? academicYearId)
    {
        var query = _context.ScheduleEntries
            .Include(se => se.TimeSlot)
            .Include(se => se.TeacherAssignment)
            .Where(se => se.TeacherAssignment.SubjectAssignmentId == subjectAssignmentId);
        if (academicYearId.HasValue)
            query = query.Where(se => se.AcademicYearId == academicYearId.Value);
        return await query.ToListAsync();
    }

    private static bool HasScheduleConflict(IEnumerable<ScheduleEntry> current, IEnumerable<ScheduleEntry> candidate)
    {
        return current.Any(a => candidate.Any(b =>
            a.DayOfWeek == b.DayOfWeek &&
            a.TimeSlot.StartTime < b.TimeSlot.EndTime &&
            b.TimeSlot.StartTime < a.TimeSlot.EndTime));
    }

    private async Task<IReadOnlyList<CelosamSelectedSubjectDto>> GetSelectedSubjectsAsync(Guid prematriculationId)
    {
        var selections = await _context.StudentPrematriculationSubjectSelections
            .Include(s => s.CurriculumSubject).ThenInclude(cs => cs.Subject)
            .Include(s => s.CurriculumSubject).ThenInclude(cs => cs.GradeLevel)
            .Include(s => s.Group)
            .Include(s => s.TeacherAssignment).ThenInclude(ta => ta!.Teacher)
            .Where(s => s.PrematriculationId == prematriculationId && s.Status != "Removed")
            .OrderBy(s => s.CurriculumSubject.ModuleOrder)
            .ToListAsync();

        var result = new List<CelosamSelectedSubjectDto>();
        foreach (var selection in selections)
        {
            var scheduleText = selection.SubjectAssignmentId.HasValue
                ? string.Join("; ", (await GetScheduleEntriesAsync(selection.SubjectAssignmentId.Value, null))
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.TimeSlot.StartTime)
                    .Select(s => $"{DayName(s.DayOfWeek)} {s.TimeSlot.StartTime:HH\\:mm}-{s.TimeSlot.EndTime:HH\\:mm}"))
                : "Pendiente de asignacion";

            result.Add(new CelosamSelectedSubjectDto(
                selection.Id,
                selection.CurriculumSubjectId,
                selection.CurriculumSubject.Subject.Name,
                selection.CurriculumSubject.GradeLevel?.Name ?? selection.CurriculumSubject.LevelName,
                selection.Status,
                selection.Group?.Name,
                selection.TeacherAssignment?.Teacher != null
                    ? $"{selection.TeacherAssignment.Teacher.Name} {selection.TeacherAssignment.Teacher.LastName}"
                    : null,
                string.IsNullOrWhiteSpace(scheduleText) ? "Sin horario publicado" : scheduleText,
                selection.ValidationMessage));
        }

        return result;
    }

    private async Task<PrematriculationReceipt> CreateReceiptAsync(Prematriculation prematriculation)
    {
        var year = DateTime.UtcNow.Year;
        var baseSequence = await _context.PrematriculationReceipts
            .IgnoreQueryFilters()
            .CountAsync(r => r.SchoolId == prematriculation.SchoolId && r.GeneratedAt.Year == year) + 1;
        var version = await _context.PrematriculationReceipts
            .CountAsync(r => r.PrematriculationId == prematriculation.Id) + 1;

        var receipt = new PrematriculationReceipt
        {
            Id = Guid.NewGuid(),
            SchoolId = prematriculation.SchoolId,
            PrematriculationId = prematriculation.Id,
            PrematriculationPeriodId = prematriculation.PrematriculationPeriodId,
            StudentId = prematriculation.StudentId,
            Consecutive = $"PM-{year}-{baseSequence:000000} V{version}",
            Version = version,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = await _currentUserService.GetCurrentUserIdAsync()
        };
        _context.PrematriculationReceipts.Add(receipt);
        return receipt;
    }

    private static string DayName(byte day) => day switch
    {
        1 => "Lunes",
        2 => "Martes",
        3 => "Miercoles",
        4 => "Jueves",
        5 => "Viernes",
        6 => "Sabado",
        7 => "Domingo",
        _ => $"Dia {day}"
    };
}

public class SubjectWithdrawalRequestService : ISubjectWithdrawalRequestService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubjectWithdrawalRequestService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private async Task AddAuditAsync(Guid schoolId, Guid? userId, string action, string resource, string details)
    {
        var user = userId.HasValue
            ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value)
            : null;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = userId,
            UserName = user == null ? null : $"{user.Name} {user.LastName}".Trim(),
            UserRole = user?.Role,
            Action = action,
            Resource = resource,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task<(bool Success, string Message)> RequestAsync(Guid studentSubjectAssignmentId, string reason, string? observation)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Debe indicar el motivo del retiro.");

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return (false, "No se pudo resolver la escuela actual.");

        var enrollment = await _context.StudentSubjectAssignments.AsNoTracking()
            .FirstOrDefaultAsync(ssa => ssa.Id == studentSubjectAssignmentId && ssa.IsActive);
        if (enrollment == null)
            return (false, "Inscripcion activa no encontrada.");

        var exists = await _context.StudentSubjectWithdrawalRequests.AnyAsync(r =>
            r.StudentSubjectAssignmentId == studentSubjectAssignmentId && r.Status == "Pending");
        if (exists)
            return (false, "Ya existe una solicitud pendiente para esta materia.");

        var request = new StudentSubjectWithdrawalRequest
        {
            Id = Guid.NewGuid(),
            SchoolId = currentUser.SchoolId.Value,
            StudentSubjectAssignmentId = enrollment.Id,
            StudentId = enrollment.StudentId,
            SubjectAssignmentId = enrollment.SubjectAssignmentId,
            RequestedBy = currentUser.Id,
            Reason = reason.Trim(),
            Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.StudentSubjectWithdrawalRequests.Add(request);
        await AddAuditAsync(
            currentUser.SchoolId.Value,
            currentUser.Id,
            "SubjectWithdrawal.Requested",
            "StudentSubjectWithdrawalRequest",
            $"Solicitud {request.Id}: retiro solicitado para inscripción {enrollment.Id}. Motivo: {reason.Trim()}");

        await _context.SaveChangesAsync();
        return (true, "Solicitud de retiro enviada al Director.");
    }

    public async Task<IReadOnlyList<StudentSubjectWithdrawalRequest>> GetPendingAsync()
    {
        return await _context.StudentSubjectWithdrawalRequests
            .Include(r => r.Student)
            .Include(r => r.SubjectAssignment).ThenInclude(sa => sa.Subject)
            .Include(r => r.SubjectAssignment).ThenInclude(sa => sa.Group)
            .Include(r => r.RequestedByUser)
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> ReviewAsync(Guid requestId, bool approve, string? reviewObservation)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return (false, "Usuario actual no encontrado.");

        var request = await _context.StudentSubjectWithdrawalRequests
            .Include(r => r.StudentSubjectAssignment)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        if (request == null)
            return (false, "Solicitud no encontrada.");
        if (request.Status != "Pending")
            return (false, "La solicitud ya fue revisada.");

        request.Status = approve ? "Approved" : "Rejected";
        request.ReviewedBy = currentUser.Id;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewObservation = string.IsNullOrWhiteSpace(reviewObservation) ? null : reviewObservation.Trim();

        if (approve)
        {
            request.StudentSubjectAssignment.Status = "Withdrawn";
            request.StudentSubjectAssignment.IsActive = false;
            request.StudentSubjectAssignment.EndDate = DateTime.UtcNow;
            request.StudentSubjectAssignment.UpdatedAt = DateTime.UtcNow;
            request.StudentSubjectAssignment.UpdatedBy = currentUser.Id;
        }

        await AddAuditAsync(
            request.SchoolId,
            currentUser.Id,
            approve ? "SubjectWithdrawal.Approved" : "SubjectWithdrawal.Rejected",
            "StudentSubjectWithdrawalRequest",
            $"Solicitud {request.Id}: {(approve ? "aprobada" : "rechazada")}. Observación: {request.ReviewObservation ?? "Sin observación"}");

        await _context.SaveChangesAsync();
        return (true, approve ? "Retiro aprobado." : "Retiro rechazado.");
    }
}
