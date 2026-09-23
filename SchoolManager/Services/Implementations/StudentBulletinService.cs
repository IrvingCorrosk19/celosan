using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class StudentBulletinService : IStudentBulletinService
{
    private readonly SchoolDbContext _context;
    private readonly IAcademicYearService _academicYearService;
    private readonly IOfficialGradeService _officialGradeService;

    public StudentBulletinService(
        SchoolDbContext context,
        IAcademicYearService academicYearService,
        IOfficialGradeService officialGradeService)
    {
        _context = context;
        _academicYearService = academicYearService;
        _officialGradeService = officialGradeService;
    }

    public async Task<StudentBulletinDto?> GetByGradeBulletinAsync(Guid studentId)
    {
        if (studentId == Guid.Empty)
            return null;

        var student = await _context.Users.AsNoTracking()
            .Where(u => u.Id == studentId)
            .Select(u => new { u.Id, u.Name, u.LastName, u.SchoolId })
            .FirstOrDefaultAsync();

        if (student == null)
            return null;

        var assignments = await _context.StudentAssignments.AsNoTracking()
            .Where(sa => sa.StudentId == studentId && sa.IsActive)
            .Include(sa => sa.Grade)
            .Include(sa => sa.Group)
            .ToListAsync();

        var primary = ActiveStudentAssignmentHelper.GetPrimaryEnrollment(assignments);
        if (primary == null)
        {
            return new StudentBulletinDto
            {
                StudentId = student.Id,
                StudentName = FormatName(student.Name, student.LastName),
                Grade = "Sin asignación",
                Group = "—",
                Specialty = "—",
                AcademicYear = "—"
            };
        }

        var gradeId = primary.GradeId;
        var assignmentIds = assignments
            .Where(a => a.GradeId == gradeId)
            .Select(a => a.Id)
            .ToHashSet();

        var enrollments = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
            .Join(_context.SubjectAssignments.AsNoTracking(),
                ssa => ssa.SubjectAssignmentId,
                sa => sa.Id,
                (ssa, sa) => new
                {
                    SsaId = ssa.Id,
                    ssa.StudentAssignmentId,
                    sa.SubjectId,
                    sa.AreaId,
                    sa.SpecialtyId,
                    sa.GradeLevelId,
                    sa.GroupId,
                    SubjectName = sa.Subject.Name,
                    AreaName = sa.Area.Name,
                    SpecialtyName = sa.Specialty.Name
                })
            .Where(x => x.GradeLevelId == gradeId)
            .ToListAsync();

        var ssaIds = enrollments.Select(e => e.SsaId).ToHashSet();

        var activeYear = student.SchoolId.HasValue
            ? await _academicYearService.GetActiveAcademicYearAsync(student.SchoolId.Value)
            : null;

        // Año: encabezado informativo. No se filtra score.academic_year_id porque
        // en datos reales el mismo SSA puede tener varios year ids; el aislamiento
        // lo dan student_id + SSA/matrícula del alumno.
        var scoresQuery = _context.StudentActivityScores.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Where(s =>
                (s.StudentSubjectAssignmentId.HasValue && ssaIds.Contains(s.StudentSubjectAssignmentId.Value))
                || assignmentIds.Contains(s.StudentAssignmentId));

        var rawScores = await scoresQuery
            .Join(_context.Activities.AsNoTracking(),
                score => score.ActivityId,
                activity => activity.Id,
                (score, activity) => new
                {
                    score.StudentId,
                    score.StudentAssignmentId,
                    score.StudentSubjectAssignmentId,
                    score.Score,
                    activity.SubjectId,
                    activity.Type,
                    activity.Trimester
                })
            .Where(x => x.StudentId == studentId)
            .ToListAsync();

        var subjects = enrollments
            .GroupBy(e => e.SubjectId)
            .Select(g => g.First())
            .ToList();

        var specialtyId = subjects
            .GroupBy(s => s.SpecialtyId)
            .OrderByDescending(g => g.Count())
            .Select(g => (Guid?)g.Key)
            .FirstOrDefault();

        if (student.SchoolId.HasValue && specialtyId.HasValue)
        {
            var catalog = await _context.CurriculumLoadSubjects.AsNoTracking()
                .Where(c =>
                    c.SchoolId == student.SchoolId.Value &&
                    c.SpecialtyId == specialtyId.Value &&
                    c.GradeLevelId == gradeId &&
                    c.IsActive)
                .Select(c => new
                {
                    c.SubjectId,
                    c.AreaId,
                    SubjectName = c.Subject.Name,
                    AreaName = c.Area.Name,
                    SpecialtyName = c.Specialty.Name,
                    c.SpecialtyId
                })
                .ToListAsync();

            foreach (var row in catalog)
            {
                if (subjects.Any(s => s.SubjectId == row.SubjectId))
                    continue;

                subjects.Add(new
                {
                    SsaId = Guid.Empty,
                    StudentAssignmentId = (Guid?)null,
                    row.SubjectId,
                    row.AreaId,
                    row.SpecialtyId,
                    GradeLevelId = gradeId,
                    GroupId = primary.GroupId,
                    row.SubjectName,
                    row.AreaName,
                    row.SpecialtyName
                });
            }
        }

        var importedRows = await _officialGradeService.GetImportedForStudentAsync(studentId);
        var importedByKey = importedRows
            .GroupBy(r => (r.StudentSubjectAssignmentId, r.AcademicYearId, r.TrimesterCode))
            .ToDictionary(g => g.Key, g => g.First());

        var bulletinSubjectIds = subjects.Select(s => s.SubjectId).Distinct().ToList();
        var contextActivities = bulletinSubjectIds.Count == 0
            ? new List<(Guid? SubjectId, string? Trimester, string? Type)>()
            : (await _context.Activities.AsNoTracking()
                .Where(a => a.SubjectId.HasValue && bulletinSubjectIds.Contains(a.SubjectId.Value)
                            && (a.GroupId == null || a.GroupId == primary.GroupId))
                .Select(a => new { a.SubjectId, a.Trimester, a.Type })
                .ToListAsync())
                .Select(a => (SubjectId: (Guid?)a.SubjectId, Trimester: (string?)a.Trimester, Type: (string?)a.Type))
                .ToList();

        var bulletinSubjects = subjects
            .Select(subject =>
            {
                var subjectScores = rawScores
                    .Where(s =>
                        s.StudentId == studentId &&
                        s.SubjectId == subject.SubjectId &&
                        (
                            (s.StudentSubjectAssignmentId.HasValue && ssaIds.Contains(s.StudentSubjectAssignmentId.Value))
                            || assignmentIds.Contains(s.StudentAssignmentId)
                        ))
                    .ToList();

                OfficialTrimesterGradeResult ForTrimester(string code)
                {
                    if (subject.SsaId != Guid.Empty && activeYear != null &&
                        importedByKey.TryGetValue((subject.SsaId, activeYear.Id, code), out var imported))
                        return OfficialFromImported(imported);

                    var slice = subjectScores
                        .Where(s => NormalizeTrimester(s.Trimester) == code)
                        .Select(s => ((string?)s.Type, s.Score));
                    var contextTypes = contextActivities
                        .Where(a => a.SubjectId == subject.SubjectId && NormalizeTrimester(a.Trimester) == code)
                        .Select(a => a.Type);
                    return _officialGradeService.CalculateFromActivities(slice, activeYear?.Name, code, contextTypes);
                }

                var t1 = ForTrimester("1T");
                var t2 = ForTrimester("2T");
                var t3 = ForTrimester("3T");

                return new
                {
                    subject.AreaId,
                    subject.AreaName,
                    subject.SubjectId,
                    subject.SubjectName,
                    T1 = t1.Score,
                    T2 = t2.Score,
                    T3 = t3.Score,
                    T1Display = t1.Display,
                    T2Display = t2.Display,
                    T3Display = t3.Display,
                    FinalAverage = OfficialTrimesterAverageCalculator.ComputeFinalAverage(t1.Score, t2.Score, t3.Score)
                };
            })
            .ToList();

        var areas = bulletinSubjects
            .GroupBy(s => new { s.AreaId, s.AreaName })
            .OrderBy(g => g.Key.AreaName)
            .Select(g => new AreaBulletinDto
            {
                AreaId = g.Key.AreaId,
                AreaName = string.IsNullOrWhiteSpace(g.Key.AreaName) ? "Sin área" : g.Key.AreaName,
                Subjects = g
                    .OrderBy(s => s.SubjectName)
                    .Select(s => new SubjectBulletinDto
                    {
                        SubjectId = s.SubjectId,
                        SubjectName = s.SubjectName,
                        T1 = s.T1,
                        T2 = s.T2,
                        T3 = s.T3,
                        T1Display = s.T1Display,
                        T2Display = s.T2Display,
                        T3Display = s.T3Display,
                        FinalAverage = s.FinalAverage
                    })
                    .ToList()
            })
            .ToList();

        var specialtyName = subjects
            .Select(s => s.SpecialtyName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        return new StudentBulletinDto
        {
            StudentId = student.Id,
            StudentName = FormatName(student.Name, student.LastName),
            Grade = primary.Grade?.Name ?? "—",
            Group = primary.Group?.Name ?? "—",
            Specialty = string.IsNullOrWhiteSpace(specialtyName) ? "—" : specialtyName,
            AcademicYear = activeYear?.Name ?? "—",
            Areas = areas,
            PendingPremedia = await LoadPendingPremediaAsync(
                student.Id,
                primary.GradeId,
                ParseGradeNumber(primary.Grade?.Name),
                activeYear,
                importedByKey)
        };
    }

    public async Task<StudentProgramHistoryDto?> GetProgramHistoryAsync(Guid studentId)
    {
        if (studentId == Guid.Empty)
            return null;

        var student = await _context.Users.AsNoTracking()
            .Where(u => u.Id == studentId)
            .Select(u => new { u.Id, u.Name, u.LastName, u.SchoolId })
            .FirstOrDefaultAsync();

        if (student == null)
            return null;

        var empty = new StudentProgramHistoryDto
        {
            StudentId = student.Id,
            StudentName = FormatName(student.Name, student.LastName)
        };

        var assignments = await _context.StudentAssignments.AsNoTracking()
            .Where(sa => sa.StudentId == studentId)
            .Include(sa => sa.Grade)
            .Include(sa => sa.AcademicYear)
            .OrderBy(sa => sa.CreatedAt)
            .ToListAsync();

        if (assignments.Count == 0)
            return empty;

        var assignmentIds = assignments.Select(a => a.Id).ToHashSet();

        var enrollments = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => ssa.StudentId == studentId && ssa.StudentAssignmentId.HasValue)
            .Where(ssa => assignmentIds.Contains(ssa.StudentAssignmentId!.Value))
            .Join(_context.SubjectAssignments.AsNoTracking(),
                ssa => ssa.SubjectAssignmentId,
                suba => suba.Id,
                (ssa, suba) => new
                {
                    SsaId = ssa.Id,
                    ssa.StudentAssignmentId,
                    suba.SubjectId,
                    suba.AreaId,
                    suba.SpecialtyId,
                    SubjectName = suba.Subject.Name,
                    AreaName = suba.Area.Name,
                    SpecialtyName = suba.Specialty.Name
                })
            .ToListAsync();

        var assignmentById = assignments.ToDictionary(a => a.Id);
        var ssaIds = enrollments.Select(e => e.SsaId).ToHashSet();

        var rawScores = await _context.StudentActivityScores.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Where(s =>
                (s.StudentSubjectAssignmentId.HasValue && ssaIds.Contains(s.StudentSubjectAssignmentId.Value))
                || assignmentIds.Contains(s.StudentAssignmentId))
            .Join(_context.Activities.AsNoTracking(),
                score => score.ActivityId,
                activity => activity.Id,
                (score, activity) => new
                {
                    score.StudentId,
                    score.StudentAssignmentId,
                    score.StudentSubjectAssignmentId,
                    score.Score,
                    activity.SubjectId,
                    activity.Type,
                    activity.Trimester
                })
            .Where(x => x.StudentId == studentId)
            .ToListAsync();

        var importedRows = await _officialGradeService.GetImportedForStudentAsync(studentId);
        var importedByKey = importedRows
            .GroupBy(r => (r.StudentSubjectAssignmentId, r.AcademicYearId, r.TrimesterCode))
            .ToDictionary(g => g.Key, g => g.First());

        var schoolGrades = student.SchoolId.HasValue
            ? await _context.GradeLevels.AsNoTracking()
                .Where(g => g.SchoolId == student.SchoolId)
                .ToListAsync()
            : new List<GradeLevel>();

        Guid? ResolveGradeId(int number) =>
            schoolGrades
                .Where(g => ParseGradeNumber(g.Name) == number)
                .Select(g => (Guid?)g.Id)
                .FirstOrDefault();

        var enrollmentRows = new List<HistoryEnrollmentRow>();
        foreach (var e in enrollments)
        {
            if (!e.StudentAssignmentId.HasValue || !assignmentById.TryGetValue(e.StudentAssignmentId.Value, out var sa))
                continue;

            var gradeNumber = ParseGradeNumber(sa.Grade?.Name);
            if (gradeNumber == 0)
                continue;

            enrollmentRows.Add(new HistoryEnrollmentRow(
                e.SsaId,
                sa.Id,
                e.SubjectId,
                e.AreaId,
                e.SpecialtyId,
                sa.GradeId,
                gradeNumber,
                e.SubjectName ?? "—",
                e.AreaName ?? "Sin área",
                e.SpecialtyName ?? "—",
                sa.AcademicYear?.Name,
                sa.AcademicYearId));
        }

        var catalogGradeNumbersBySpecialty = new Dictionary<Guid, List<int>>();
        if (student.SchoolId.HasValue)
        {
            var catalogGradeRows = await _context.CurriculumLoadSubjects.AsNoTracking()
                .Where(c => c.SchoolId == student.SchoolId.Value && c.IsActive)
                .Select(c => new { c.SpecialtyId, GradeName = c.GradeLevel.Name })
                .ToListAsync();

            catalogGradeNumbersBySpecialty = catalogGradeRows
                .GroupBy(c => c.SpecialtyId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => ParseGradeNumber(x.GradeName)).Where(n => n > 0).Distinct().ToList());
        }

        var assignmentNumbers = assignments
            .Select(a => ParseGradeNumber(a.Grade?.Name))
            .Where(n => n > 0)
            .Distinct()
            .ToList();

        var tracks = new List<ProgramHistoryTrackDto>();
        foreach (var specialtyGroup in enrollmentRows.GroupBy(r => r.SpecialtyId))
        {
            var sample = specialtyGroup.First();
            var enrolledNumbers = specialtyGroup.Select(r => r.GradeNumber).Distinct().ToList();
            catalogGradeNumbersBySpecialty.TryGetValue(specialtyGroup.Key, out var catalogNumbers);

            var bands = ResolveTrackBands(catalogNumbers, enrolledNumbers, assignmentNumbers);
            if (bands.Count == 0)
                continue;

            foreach (var (programType, gradeNumbers) in bands)
            {
                var bandRows = specialtyGroup
                    .Where(r => gradeNumbers.Contains(r.GradeNumber))
                    .ToList();

                var columns = gradeNumbers.Select(n =>
                {
                    var rowForGrade = bandRows
                        .Where(r => r.GradeNumber == n)
                        .FirstOrDefault();
                    var year = assignments
                        .Where(a => ParseGradeNumber(a.Grade?.Name) == n)
                        .OrderByDescending(a => a.IsActive)
                        .ThenByDescending(a => a.CreatedAt)
                        .Select(a => a.AcademicYear?.Name)
                        .FirstOrDefault(y => !string.IsNullOrWhiteSpace(y));
                    return new ProgramHistoryGradeColumnDto
                    {
                        GradeId = rowForGrade?.GradeId ?? ResolveGradeId(n),
                        GradeNumber = n,
                        GradeName = CurriculumLoadLevel.FormatGradeLabel(n),
                        AcademicYear = string.IsNullOrWhiteSpace(year) ? "—" : year!
                    };
                }).ToList();

                var subjects = bandRows
                    .GroupBy(r => r.SubjectId)
                    .Select(g => g.First())
                    .ToList();

                if (student.SchoolId.HasValue)
                {
                    var gradeIdsForCatalog = columns
                        .Where(c => c.GradeId.HasValue)
                        .Select(c => c.GradeId!.Value)
                        .Distinct()
                        .ToList();

                    if (gradeIdsForCatalog.Count > 0)
                    {
                        var catalog = await _context.CurriculumLoadSubjects.AsNoTracking()
                            .Where(c =>
                                c.SchoolId == student.SchoolId.Value &&
                                c.SpecialtyId == specialtyGroup.Key &&
                                gradeIdsForCatalog.Contains(c.GradeLevelId) &&
                                c.IsActive)
                            .Select(c => new
                            {
                                c.SubjectId,
                                c.AreaId,
                                SubjectName = c.Subject.Name,
                                AreaName = c.Area.Name
                            })
                            .ToListAsync();

                        foreach (var row in catalog)
                        {
                            if (subjects.Any(s => s.SubjectId == row.SubjectId))
                                continue;
                            subjects.Add(new HistoryEnrollmentRow(
                                Guid.Empty,
                                Guid.Empty,
                                row.SubjectId,
                                row.AreaId,
                                specialtyGroup.Key,
                                Guid.Empty,
                                0,
                                row.SubjectName ?? "—",
                                row.AreaName ?? "Sin área",
                                sample.SpecialtyName,
                                null,
                                null));
                        }
                    }
                }

                var areaDtos = subjects
                    .GroupBy(s => new { s.AreaId, s.AreaName })
                    .OrderBy(g => g.Key.AreaName)
                    .Select(areaGroup => new ProgramHistoryAreaDto
                    {
                        AreaId = areaGroup.Key.AreaId,
                        AreaName = string.IsNullOrWhiteSpace(areaGroup.Key.AreaName) ? "Sin área" : areaGroup.Key.AreaName,
                        Subjects = areaGroup
                            .OrderBy(s => s.SubjectName)
                            .Select(subject =>
                            {
                                var results = columns.Select(col =>
                                {
                                    var gradeSsaIds = bandRows
                                        .Where(r => r.SubjectId == subject.SubjectId && r.GradeNumber == col.GradeNumber)
                                        .Select(r => r.SsaId)
                                        .Where(id => id != Guid.Empty)
                                        .ToHashSet();
                                    var gradeAssignmentIds = assignments
                                        .Where(a => ParseGradeNumber(a.Grade?.Name) == col.GradeNumber)
                                        .Select(a => a.Id)
                                        .ToHashSet();

                                    var slice = rawScores.Where(s =>
                                        s.StudentId == studentId &&
                                        s.SubjectId == subject.SubjectId &&
                                        (
                                            (s.StudentSubjectAssignmentId.HasValue && gradeSsaIds.Contains(s.StudentSubjectAssignmentId.Value))
                                            || gradeAssignmentIds.Contains(s.StudentAssignmentId)
                                        )).ToList();

                                    OfficialTrimesterGradeResult ForTrimester(string code)
                                    {
                                        foreach (var ssaId in gradeSsaIds)
                                        {
                                            var yearId = bandRows
                                                .Where(r => r.SsaId == ssaId)
                                                .Select(r => r.AcademicYearId)
                                                .FirstOrDefault();
                                            if (yearId.HasValue &&
                                                importedByKey.TryGetValue((ssaId, yearId.Value, code), out var imported))
                                                return OfficialFromImported(imported);
                                        }

                                        var tri = slice
                                            .Where(s => NormalizeTrimester(s.Trimester) == code)
                                            .Select(s => (Type: (string?)s.Type, Score: s.Score))
                                            .ToList();
                                        return _officialGradeService.CalculateFromActivities(tri, col.AcademicYear, code, tri.Select(s => s.Type));
                                    }

                                    var t1 = ForTrimester("1T");
                                    var t2 = ForTrimester("2T");
                                    var t3 = ForTrimester("3T");
                                    var finalAverage = OfficialTrimesterAverageCalculator.ComputeFinalAverage(t1.Score, t2.Score, t3.Score);

                                    return new ProgramHistoryGradeResultDto
                                    {
                                        GradeNumber = col.GradeNumber,
                                        GradeId = col.GradeId,
                                        GradeName = col.GradeName,
                                        AcademicYear = col.AcademicYear,
                                        FinalAverage = finalAverage,
                                        Display = YearCellDisplay(finalAverage, t1, t2, t3)
                                    };
                                }).ToList();

                                return new ProgramHistorySubjectDto
                                {
                                    SubjectId = subject.SubjectId,
                                    SubjectName = subject.SubjectName,
                                    GradeResults = results
                                };
                            })
                            .ToList()
                    })
                    .Where(a => a.Subjects.Count > 0)
                    .ToList();

                tracks.Add(new ProgramHistoryTrackDto
                {
                    SpecialtyId = specialtyGroup.Key,
                    ProgramType = programType,
                    ProgramName = CurriculumLoadLevel.HeaderTitle(programType, sample.SpecialtyName),
                    Grades = columns,
                    Areas = areaDtos
                });
            }
        }

        var primary = ActiveStudentAssignmentHelper.GetPrimaryEnrollment(
            assignments.Where(a => a.IsActive));
        var pending = primary == null
            ? new List<PendingPremediaSubjectDto>()
            : await LoadPendingPremediaAsync(
                student.Id,
                primary.GradeId,
                ParseGradeNumber(primary.Grade?.Name),
                student.SchoolId.HasValue
                    ? await _academicYearService.GetActiveAcademicYearAsync(student.SchoolId.Value)
                    : null,
                importedByKey);

        return new StudentProgramHistoryDto
        {
            StudentId = student.Id,
            StudentName = FormatName(student.Name, student.LastName),
            Tracks = tracks,
            PendingPremedia = pending
        };
    }

    private sealed record HistoryEnrollmentRow(
        Guid SsaId,
        Guid StudentAssignmentId,
        Guid SubjectId,
        Guid AreaId,
        Guid SpecialtyId,
        Guid GradeId,
        int GradeNumber,
        string SubjectName,
        string AreaName,
        string SpecialtyName,
        string? AcademicYearName,
        Guid? AcademicYearId);

    /// <summary>
    /// Un ciclo por track según estructura académica (catálogo de la especialidad),
    /// sin mezclar Premedia (7-9) y Media (10-12). Siempre expone los 3 grados del ciclo.
    /// </summary>
    private static List<(string Type, IReadOnlyList<int> Numbers)> ResolveTrackBands(
        IReadOnlyCollection<int>? catalogNumbers,
        IReadOnlyCollection<int> enrolledNumbers,
        IReadOnlyCollection<int> assignmentNumbers)
    {
        static string? ExclusiveCycle(IEnumerable<int>? nums)
        {
            if (nums == null)
                return null;

            var list = nums.Where(n => n > 0).ToList();
            var premedia = list.Any(CurriculumLoadLevel.IsPremediaGrade);
            var media = list.Any(CurriculumLoadLevel.IsMediaGrade);
            if (premedia && !media)
                return CurriculumLoadLevel.Premedia;
            if (media && !premedia)
                return CurriculumLoadLevel.Media;
            return null;
        }

        var single = ExclusiveCycle(catalogNumbers)
                     ?? ExclusiveCycle(enrolledNumbers)
                     ?? ExclusiveCycle(assignmentNumbers);

        if (single != null)
        {
            return new List<(string Type, IReadOnlyList<int> Numbers)>
            {
                (single, CurriculumLoadLevel.ExpectedGrades(single))
            };
        }

        var bands = new List<(string Type, IReadOnlyList<int> Numbers)>();
        var combined = (catalogNumbers ?? Array.Empty<int>())
            .Concat(enrolledNumbers)
            .Concat(assignmentNumbers)
            .ToList();
        if (combined.Any(CurriculumLoadLevel.IsPremediaGrade))
            bands.Add((CurriculumLoadLevel.Premedia, CurriculumLoadLevel.ExpectedGrades(CurriculumLoadLevel.Premedia)));
        if (combined.Any(CurriculumLoadLevel.IsMediaGrade))
            bands.Add((CurriculumLoadLevel.Media, CurriculumLoadLevel.ExpectedGrades(CurriculumLoadLevel.Media)));
        return bands;
    }

    private async Task<List<PendingPremediaSubjectDto>> LoadPendingPremediaAsync(
        Guid studentId,
        Guid primaryGradeId,
        int primaryGradeNumber,
        AcademicYear? activeYear,
        IReadOnlyDictionary<(Guid StudentSubjectAssignmentId, Guid AcademicYearId, string TrimesterCode), ImportedOfficialGradeRow> importedByKey)
    {
        if (!CurriculumLoadLevel.IsMediaGrade(primaryGradeNumber))
            return new List<PendingPremediaSubjectDto>();

        var student = await _context.Users.AsNoTracking()
            .Where(u => u.Id == studentId)
            .Select(u => new { u.SchoolId })
            .FirstOrDefaultAsync();
        if (student?.SchoolId == null)
            return new List<PendingPremediaSubjectDto>();

        var gradeLevels = (await _context.GradeLevels.AsNoTracking()
            .Where(g => g.SchoolId == null || g.SchoolId == student.SchoolId.Value)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync())
            .Select(g => new { g.Id, g.Name, Number = ParseGradeNumber(g.Name) })
            .Where(g => CurriculumLoadLevel.IsPremediaGrade(g.Number))
            .ToList();
        var premediaGradeIds = gradeLevels.Select(g => g.Id).ToHashSet();
        if (premediaGradeIds.Count == 0)
            return new List<PendingPremediaSubjectDto>();

        var plan = (await _context.CurriculumLoadSubjects.AsNoTracking()
            .Where(c =>
                c.SchoolId == student.SchoolId.Value &&
                c.IsActive &&
                premediaGradeIds.Contains(c.GradeLevelId))
            .Select(c => new
            {
                c.SubjectId,
                c.AreaId,
                c.GradeLevelId,
                SubjectName = c.Subject.Name,
                AreaName = c.Area.Name,
                GradeName = c.GradeLevel.Name
            })
            .ToListAsync())
            .GroupBy(c => new { c.SubjectId, c.GradeLevelId })
            .Select(g => g.First())
            .ToList();

        var enrollments = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => ssa.StudentId == studentId)
            .Join(_context.SubjectAssignments.AsNoTracking(),
                ssa => ssa.SubjectAssignmentId,
                sa => sa.Id,
                (ssa, sa) => new
                {
                    SsaId = ssa.Id,
                    ssa.StudentAssignmentId,
                    ssa.AcademicYearId,
                    ssa.IsActive,
                    ssa.EnrollmentType,
                    sa.SubjectId,
                    sa.AreaId,
                    sa.GradeLevelId,
                    sa.GroupId,
                    SubjectName = sa.Subject.Name,
                    AreaName = sa.Area.Name,
                    GradeName = sa.GradeLevel.Name
                })
            .ToListAsync();

        var premediaEnrollments = enrollments
            .Where(e => premediaGradeIds.Contains(e.GradeLevelId) && e.GradeLevelId != primaryGradeId)
            .ToList();

        var candidates = plan
            .Select(p => new
            {
                p.SubjectId,
                p.GradeLevelId,
                p.AreaId,
                p.SubjectName,
                p.AreaName,
                p.GradeName,
                GradeNumber = ParseGradeNumber(p.GradeName),
                Enrollment = premediaEnrollments
                    .Where(e => e.SubjectId == p.SubjectId && e.GradeLevelId == p.GradeLevelId)
                    .OrderByDescending(e => e.IsActive)
                    .FirstOrDefault()
            })
            .ToList();

        foreach (var extra in premediaEnrollments.Where(e => e.IsActive && EnrollmentTypeConstants.IsCarryOver(e.EnrollmentType)))
        {
            if (candidates.Any(c => c.SubjectId == extra.SubjectId && c.GradeLevelId == extra.GradeLevelId))
                continue;
            candidates.Add(new
            {
                extra.SubjectId,
                extra.GradeLevelId,
                extra.AreaId,
                extra.SubjectName,
                extra.AreaName,
                extra.GradeName,
                GradeNumber = ParseGradeNumber(extra.GradeName),
                Enrollment = premediaEnrollments.FirstOrDefault(e => e.SsaId == extra.SsaId)
            });
        }

        if (plan.Count == 0)
        {
            var primaryRegularGradeIds = (await _context.StudentAssignments.AsNoTracking()
                .Where(sa => sa.StudentId == studentId && sa.IsActive)
                .Select(sa => new { sa.GradeId, sa.EnrollmentType })
                .ToListAsync())
                .Where(sa => EnrollmentTypeConstants.IsPrimaryLevel(sa.EnrollmentType))
                .Select(sa => sa.GradeId)
                .Distinct()
                .ToHashSet();

            candidates = premediaEnrollments
                .Where(e => e.IsActive && BulletinPendingPremedia.IsPending(
                    primaryGradeNumber,
                    ParseGradeNumber(e.GradeName),
                    e.IsActive,
                    e.EnrollmentType,
                    primaryRegularGradeIds.Contains(e.GradeLevelId)))
                .GroupBy(e => new { e.SubjectId, e.GradeLevelId })
                .Select(g => g.First())
                .Select(e => new
                {
                    e.SubjectId,
                    e.GradeLevelId,
                    e.AreaId,
                    e.SubjectName,
                    e.AreaName,
                    e.GradeName,
                    GradeNumber = ParseGradeNumber(e.GradeName),
                    Enrollment = premediaEnrollments.FirstOrDefault(x => x.SsaId == e.SsaId)
                })
                .ToList();
        }

        if (candidates.Count == 0)
            return new List<PendingPremediaSubjectDto>();

        var ssaIds = candidates
            .Where(c => c.Enrollment != null)
            .Select(c => c.Enrollment!.SsaId)
            .ToHashSet();
        var assignmentIds = candidates
            .Where(c => c.Enrollment?.StudentAssignmentId != null)
            .Select(c => c.Enrollment!.StudentAssignmentId!.Value)
            .ToHashSet();
        var subjectIds = candidates.Select(c => c.SubjectId).Distinct().ToList();
        var groupIds = candidates
            .Where(c => c.Enrollment != null)
            .Select(c => c.Enrollment!.GroupId)
            .Distinct()
            .ToList();

        var rawScores = await _context.StudentActivityScores.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Where(s =>
                (s.StudentSubjectAssignmentId.HasValue && ssaIds.Contains(s.StudentSubjectAssignmentId.Value))
                || assignmentIds.Contains(s.StudentAssignmentId))
            .Join(_context.Activities.AsNoTracking(),
                score => score.ActivityId,
                activity => activity.Id,
                (score, activity) => new
                {
                    score.StudentSubjectAssignmentId,
                    score.StudentAssignmentId,
                    score.Score,
                    activity.SubjectId,
                    activity.Type,
                    activity.Trimester
                })
            .ToListAsync();

        var contextActivities = subjectIds.Count == 0
            ? new List<(Guid? SubjectId, Guid? GroupId, string? Trimester, string? Type)>()
            : (await _context.Activities.AsNoTracking()
                .Where(a => a.SubjectId.HasValue && subjectIds.Contains(a.SubjectId.Value)
                            && (a.GroupId == null || groupIds.Contains(a.GroupId.Value)))
                .Select(a => new { a.SubjectId, a.GroupId, a.Trimester, a.Type })
                .ToListAsync())
                .Select(a => (SubjectId: (Guid?)a.SubjectId, GroupId: a.GroupId, Trimester: (string?)a.Trimester, Type: (string?)a.Type))
                .ToList();

        var importedRows = importedByKey.Values.ToList();

        var built = candidates
            .OrderBy(p => p.GradeNumber)
            .ThenBy(p => p.SubjectName)
            .Select(p =>
            {
                var subjectScores = rawScores
                    .Where(s =>
                        s.SubjectId == p.SubjectId &&
                        (
                            p.Enrollment == null
                            || (s.StudentSubjectAssignmentId.HasValue && s.StudentSubjectAssignmentId.Value == p.Enrollment.SsaId)
                            || (p.Enrollment.StudentAssignmentId.HasValue && s.StudentAssignmentId == p.Enrollment.StudentAssignmentId.Value)
                        ))
                    .ToList();

                OfficialTrimesterGradeResult ForTrimester(string code)
                {
                    if (p.Enrollment != null)
                    {
                        if (p.Enrollment.AcademicYearId.HasValue &&
                            importedByKey.TryGetValue((p.Enrollment.SsaId, p.Enrollment.AcademicYearId.Value, code), out var importedSsaYear))
                            return OfficialFromImported(importedSsaYear);
                        if (activeYear != null &&
                            importedByKey.TryGetValue((p.Enrollment.SsaId, activeYear.Id, code), out var importedActive))
                            return OfficialFromImported(importedActive);
                    }

                    var importedBySubject = importedRows.FirstOrDefault(r =>
                        r.SubjectId == p.SubjectId && r.TrimesterCode == code);
                    if (importedBySubject != null)
                        return OfficialFromImported(importedBySubject);

                    var slice = subjectScores
                        .Where(s => NormalizeTrimester(s.Trimester) == code)
                        .Select(s => ((string?)s.Type, s.Score));
                    var contextTypes = contextActivities
                        .Where(a => a.SubjectId == p.SubjectId
                                    && NormalizeTrimester(a.Trimester) == code
                                    && (p.Enrollment == null || a.GroupId == null || a.GroupId == p.Enrollment.GroupId))
                        .Select(a => a.Type);
                    return _officialGradeService.CalculateFromActivities(slice, activeYear?.Name, code, contextTypes);
                }

                var t1 = ForTrimester("1T");
                var t2 = ForTrimester("2T");
                var t3 = ForTrimester("3T");
                var finalAverage = OfficialTrimesterAverageCalculator.ComputeFinalAverage(t1.Score, t2.Score, t3.Score);
                var isCarryOver = p.Enrollment != null && p.Enrollment.IsActive
                    && EnrollmentTypeConstants.IsCarryOver(p.Enrollment.EnrollmentType);
                if (!isCarryOver && OfficialGradeMark.IsApproved(finalAverage))
                    return null;

                return new PendingPremediaSubjectDto
                {
                    SubjectId = p.SubjectId,
                    SubjectName = string.IsNullOrWhiteSpace(p.SubjectName) ? "—" : p.SubjectName,
                    Grade = string.IsNullOrWhiteSpace(p.GradeName)
                        ? CurriculumLoadLevel.FormatGradeLabel(p.GradeNumber)
                        : p.GradeName,
                    GradeNumber = p.GradeNumber,
                    GradeLevelId = p.GradeLevelId,
                    AreaName = string.IsNullOrWhiteSpace(p.AreaName) ? "Sin área" : p.AreaName,
                    T1 = t1.Score,
                    T2 = t2.Score,
                    T3 = t3.Score,
                    T1Display = t1.Display,
                    T2Display = t2.Display,
                    T3Display = t3.Display,
                    FinalAverage = finalAverage
                };
            })
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

        return built;
    }

    private static OfficialTrimesterGradeResult OfficialFromImported(ImportedOfficialGradeRow imported) =>
        new()
        {
            Score = imported.Score,
            Status = ImportedTrimesterGradeStatus.Normalize(imported.Status),
            IsImported = true,
            Origin = OfficialTrimesterGradeOrigin.Imported,
            ImportedGradeId = imported.ImportedGradeId
        };

    private static string YearCellDisplay(
        decimal? finalAverage,
        OfficialTrimesterGradeResult t1,
        OfficialTrimesterGradeResult t2,
        OfficialTrimesterGradeResult t3)
    {
        if (finalAverage.HasValue)
            return OfficialGradeMark.Display(finalAverage);

        var marks = new[] { t1.Display, t2.Display, t3.Display }
            .Where(d => d != OfficialGradeMark.Empty)
            .ToList();
        if (marks.Count > 0 && marks.All(d => d == OfficialGradeMark.NoAsistio))
            return OfficialGradeMark.NoAsistio;
        if (marks.Count > 0 && marks.All(d => d == OfficialGradeMark.SinNota))
            return OfficialGradeMark.SinNota;
        return OfficialGradeMark.Empty;
    }


    private static int ParseGradeNumber(string? name) =>
        EnrollmentTypeConstants.ParseGradeNumber(name) ?? 0;

    private static string FormatName(string? name, string? lastName) =>
        $"{(name ?? string.Empty).Trim()} {(lastName ?? string.Empty).Trim()}".Trim();

    private static string NormalizeTrimester(string? trimester)
    {
        var t = (trimester ?? string.Empty).Trim().ToUpperInvariant();
        if (t is "T1" or "1T") return "1T";
        if (t is "T2" or "2T") return "2T";
        if (t is "T3" or "3T") return "3T";
        return t;
    }
}
