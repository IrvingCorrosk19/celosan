using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class CurriculumLoadService : ICurriculumLoadService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CurriculumLoadService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<AcademicProgramOptionDto>> GetProgramsForTeacherAsync(Guid teacherId)
    {
        var specialtyIds = await _context.TeacherAssignments
            .AsNoTracking()
            .Where(ta => ta.TeacherId == teacherId)
            .Select(ta => ta.SubjectAssignment.SpecialtyId)
            .Distinct()
            .ToListAsync();

        return await BuildProgramOptionsAsync(specialtyIds);
    }

    public async Task<List<AcademicProgramOptionDto>> GetProgramsForAdministrationAsync()
    {
        return await BuildProgramOptionsAsync(null);
    }

    public async Task<CurriculumLoadReportDto?> GetReportAsync(
        Guid programId,
        string viewMode,
        int? selectedGrade,
        bool includeInactive)
    {
        var rows = await _context.CurriculumLoadSubjects
            .AsNoTracking()
            .Where(s => s.SpecialtyId == programId)
            .Where(s => includeInactive || s.IsActive)
            .Select(s => new LoadRow
            {
                Id = s.Id,
                SpecialtyId = s.SpecialtyId,
                ProgramName = s.Specialty.Name,
                GradeLevelId = s.GradeLevelId,
                GradeName = s.GradeLevel.Name,
                AreaId = s.AreaId,
                AreaName = s.Area.Name,
                SubjectId = s.SubjectId,
                SubjectName = s.Subject.Name,
                IsActive = s.IsActive,
                Hours = s.Hours.Select(h => new LoadHour { Code = h.CurriculumBlock.Code, Hours = h.Hours }).ToList()
            })
            .ToListAsync();

        if (rows.Count == 0)
            return null;

        var gradeNumbers = rows
            .Select(r => EnrollmentTypeConstants.ParseGradeNumber(r.GradeName))
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        var programType = CurriculumLoadLevel.ResolveProgramType(gradeNumbers)
                          ?? CurriculumLoadLevel.Media;
        var expectedGrades = CurriculumLoadLevel.ExpectedGrades(programType).ToList();

        var mode = string.Equals(viewMode, CurriculumLoadLevel.ViewByGrade, StringComparison.OrdinalIgnoreCase)
            ? CurriculumLoadLevel.ViewByGrade
            : CurriculumLoadLevel.ViewFullProgram;

        if (mode == CurriculumLoadLevel.ViewByGrade)
        {
            var grade = selectedGrade ?? expectedGrades.First();
            if (!expectedGrades.Contains(grade))
                grade = expectedGrades.First();
            expectedGrades = new List<int> { grade };
            selectedGrade = grade;
        }
        else
        {
            selectedGrade = null;
        }

        var report = new CurriculumLoadReportDto
        {
            ProgramId = programId,
            ProgramName = rows[0].ProgramName,
            ProgramType = programType,
            HeaderTitle = CurriculumLoadLevel.HeaderTitle(programType, rows[0].ProgramName),
            ViewMode = mode,
            SelectedGrade = selectedGrade,
            Grades = expectedGrades
        };

        var areas = rows
            .GroupBy(r => new { r.AreaId, r.AreaName })
            .OrderBy(g => CurriculumLoadDisplayOrder.AreaRank(g.Key.AreaName))
            .ThenBy(g => g.Key.AreaName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var areaGroup in areas)
        {
            var areaDto = new CurriculumLoadAreaDto
            {
                AreaId = areaGroup.Key.AreaId,
                Name = CurriculumLoadDisplayOrder.DisplayAreaName(areaGroup.Key.AreaName)
            };

            var presentationRows = BuildPresentationRows(
                report.ProgramName,
                areaGroup.Key.AreaName,
                areaGroup,
                expectedGrades);

            foreach (var subjectDto in presentationRows)
                areaDto.Subjects.Add(subjectDto);

            areaDto.TotalsByGrade = expectedGrades.Select(grade =>
            {
                var cells = areaDto.Subjects.SelectMany(s => s.GradeLoads).Where(g => g.Grade == grade);
                return new CurriculumLoadGradeTotalDto
                {
                    Grade = grade,
                    B1 = SumList(cells.Select(c => c.B1)),
                    B2 = SumList(cells.Select(c => c.B2)),
                    Total = SumList(cells.Select(c => c.Total))
                };
            }).ToList();

            areaDto.TotalHours = SumList(areaDto.TotalsByGrade.Select(t => t.Total));
            report.Areas.Add(areaDto);
        }

        report.TotalHoursByGrade = expectedGrades.Select(grade =>
        {
            var cells = report.Areas.SelectMany(a => a.Subjects).SelectMany(s => s.GradeLoads).Where(g => g.Grade == grade);
            return new CurriculumLoadGradeTotalDto
            {
                Grade = grade,
                B1 = SumList(cells.Select(c => c.B1)),
                B2 = SumList(cells.Select(c => c.B2)),
                Total = SumList(cells.Select(c => c.Total))
            };
        }).ToList();

        report.TotalHours = SumList(report.TotalHoursByGrade.Select(t => t.Total));

        report.SubjectCountsByGrade = expectedGrades.Select(grade =>
        {
            var cells = report.Areas.SelectMany(a => a.Subjects).SelectMany(s => s.GradeLoads).Where(g => g.Grade == grade);
            return new CurriculumLoadSubjectCountDto
            {
                Grade = grade,
                B1 = cells.Count(c => c.B1.HasValue && c.B1.Value > 0),
                B2 = cells.Count(c => c.B2.HasValue && c.B2.Value > 0),
                InGrade = cells.Count(c => (c.B1 ?? 0) + (c.B2 ?? 0) > 0)
            };
        }).ToList();

        report.TotalSubjects = report.SubjectCountsByGrade.Sum(c => c.B1 + c.B2);

        return report;
    }

    public async Task<List<CurriculumLoadInactiveRowDto>> GetInactiveSubjectsAsync(Guid programId)
    {
        return await _context.CurriculumLoadSubjects
            .AsNoTracking()
            .Where(s => s.SpecialtyId == programId && !s.IsActive)
            .OrderBy(s => s.GradeLevel.Name)
            .ThenBy(s => s.Subject.Name)
            .Select(s => new CurriculumLoadInactiveRowDto
            {
                CurriculumLoadSubjectId = s.Id,
                Subject = s.Subject.Name,
                Area = s.Area.Name,
                Grade = s.GradeLevel.Name
            })
            .ToListAsync();
    }

    public async Task SaveHoursAsync(Guid curriculumLoadSubjectId, decimal? b1, decimal? b2, string? onlyBlock = null)
    {
        var subject = await _context.CurriculumLoadSubjects
            .FirstOrDefaultAsync(s => s.Id == curriculumLoadSubjectId);
        if (subject == null)
            throw new InvalidOperationException("No se encontró la asignatura curricular.");

        var blocks = await _context.CurriculumBlocks.ToListAsync();
        var b1Block = blocks.FirstOrDefault(b => b.Code == CurriculumLoadLevel.BlockB1);
        var b2Block = blocks.FirstOrDefault(b => b.Code == CurriculumLoadLevel.BlockB2);
        if (b1Block == null || b2Block == null)
            throw new InvalidOperationException("No existen los bloques B1 y B2.");

        var block = (onlyBlock ?? string.Empty).Trim().ToUpperInvariant();
        if (block == CurriculumLoadLevel.BlockB1)
        {
            await UpsertOrRemoveHoursAsync(subject.Id, b1Block.Id, b1);
        }
        else if (block == CurriculumLoadLevel.BlockB2)
        {
            await UpsertOrRemoveHoursAsync(subject.Id, b2Block.Id, b2);
        }
        else
        {
            await UpsertOrRemoveHoursAsync(subject.Id, b1Block.Id, b1);
            await UpsertOrRemoveHoursAsync(subject.Id, b2Block.Id, b2);
        }
        await AuditHelper.SetAuditFieldsForUpdateAsync(subject, _currentUserService);
        await _context.SaveChangesAsync();
    }

    public async Task SetActiveAsync(Guid curriculumLoadSubjectId, bool isActive)
    {
        var subject = await _context.CurriculumLoadSubjects
            .FirstOrDefaultAsync(s => s.Id == curriculumLoadSubjectId);
        if (subject == null)
            throw new InvalidOperationException("No se encontró la asignatura curricular.");

        subject.IsActive = isActive;
        await AuditHelper.SetAuditFieldsForUpdateAsync(subject, _currentUserService);
        await _context.SaveChangesAsync();
    }

    private async Task<List<AcademicProgramOptionDto>> BuildProgramOptionsAsync(List<Guid>? specialtyFilter)
    {
        var query = _context.CurriculumLoadSubjects.AsNoTracking().AsQueryable();
        if (specialtyFilter != null)
            query = query.Where(s => specialtyFilter.Contains(s.SpecialtyId));

        var raw = await query
            .Select(s => new
            {
                s.SpecialtyId,
                Name = s.Specialty.Name,
                GradeName = s.GradeLevel.Name
            })
            .ToListAsync();

        return raw
            .GroupBy(r => new { r.SpecialtyId, r.Name })
            .Select(g =>
            {
                var grades = g
                    .Select(x => EnrollmentTypeConstants.ParseGradeNumber(x.GradeName))
                    .Where(n => n.HasValue)
                    .Select(n => n!.Value);
                var type = CurriculumLoadLevel.ResolveProgramType(grades) ?? CurriculumLoadLevel.Media;
                return new AcademicProgramOptionDto
                {
                    ProgramId = g.Key.SpecialtyId,
                    Name = g.Key.Name,
                    ProgramType = type
                };
            })
            .OrderBy(p => p.ProgramType == CurriculumLoadLevel.Premedia ? 0 : 1)
            .ThenBy(p => p.Name)
            .ToList();
    }

    private static List<CurriculumLoadSubjectRowDto> BuildPresentationRows(
        string programName,
        string areaName,
        IEnumerable<LoadRow> areaRows,
        IReadOnlyList<int> expectedGrades)
    {
        var materialized = areaRows.ToList();
        var result = new List<CurriculumLoadSubjectRowDto>();
        var programKey = CurriculumLoadDisplayOrder.Normalize(programName);
        var isPremediaTech = CurriculumLoadDisplayOrder.AreaRank(areaName) == 3
            && (programKey.Contains("PRE-MEDIA") || programKey.Contains("PREMEDIA"));

        List<LoadRow> remaining = materialized;
        if (isPremediaTech)
        {
            var aliases = materialized.Where(r => CurriculumLoadDisplayOrder.IsPremediaTechAlias(r.SubjectName)).ToList();
            remaining = materialized.Where(r => !CurriculumLoadDisplayOrder.IsPremediaTechAlias(r.SubjectName)).ToList();
            if (aliases.Count > 0)
                result.Add(BuildPremediaTechRow(aliases, expectedGrades));
        }

        foreach (var subjectGroup in remaining.GroupBy(r => new { r.SubjectId, r.SubjectName }))
        {
            if (CurriculumLoadDisplayOrder.IsUnofficialEmptyDuplicate(programName, subjectGroup.Key.SubjectName)
                && !subjectGroup.SelectMany(r => r.Hours).Any(h => h.Hours > 0))
                continue;

            if (programKey.Contains("ELECTRICIDAD")
                && CurriculumLoadDisplayOrder.IsCombinedLogicPhilosophy(subjectGroup.Key.SubjectName))
            {
                result.Add(BuildNamedRow("LÓGICA", subjectGroup.Key.SubjectId, subjectGroup.Where(r =>
                    EnrollmentTypeConstants.ParseGradeNumber(r.GradeName) == 11), expectedGrades, programName, areaName));
                result.Add(BuildNamedRow("FILOSOFÍA", subjectGroup.Key.SubjectId, subjectGroup.Where(r =>
                    EnrollmentTypeConstants.ParseGradeNumber(r.GradeName) == 12), expectedGrades, programName, areaName));
                continue;
            }

            result.Add(BuildNamedRow(
                CurriculumLoadDisplayOrder.DisplaySubjectName(programName, subjectGroup.Key.SubjectName),
                subjectGroup.Key.SubjectId,
                subjectGroup,
                expectedGrades,
                programName,
                areaName));
        }

        return result
            .Where(s => s.GradeLoads.Any(HasCurriculumCell))
            .OrderBy(s => CurriculumLoadDisplayOrder.SubjectRank(programName, areaName, s.Subject))
            .ThenBy(s => s.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CurriculumLoadSubjectRowDto BuildPremediaTechRow(
        IReadOnlyCollection<LoadRow> aliases,
        IReadOnlyList<int> expectedGrades)
    {
        var byName = aliases.ToDictionary(
            r => CurriculumLoadDisplayOrder.Normalize(r.SubjectName),
            r => r,
            StringComparer.Ordinal);

        LoadRow? Find(string name) =>
            byName.TryGetValue(CurriculumLoadDisplayOrder.Normalize(name), out var row) ? row : null;

        var map = new Dictionary<int, (LoadRow? B1, LoadRow? B2)>
        {
            [7] = (Find("FDC 1"), Find("MET")),
            [8] = (Find("FDC 2"), Find("D.L.")),
            [9] = (Find("MCA 1"), Find("MCA 2"))
        };

        var subjectDto = new CurriculumLoadSubjectRowDto
        {
            SubjectId = aliases.First().SubjectId,
            Subject = CurriculumLoadDisplayOrder.PremediaTechDisplayName,
            NeedsReview = false
        };

        foreach (var grade in expectedGrades)
        {
            map.TryGetValue(grade, out var pair);
            var load = new CurriculumLoadGradeHoursDto { Grade = grade };
            if (pair.B1 != null)
            {
                var hour = pair.B1.Hours.FirstOrDefault(h => h.Code == CurriculumLoadLevel.BlockB1);
                load.B1 = hour?.Hours ?? 0;
                load.B1CurriculumLoadSubjectId = pair.B1.Id;
                load.CurriculumLoadSubjectId = pair.B1.Id;
                load.IsActive = pair.B1.IsActive;
            }
            if (pair.B2 != null)
            {
                var hour = pair.B2.Hours.FirstOrDefault(h => h.Code == CurriculumLoadLevel.BlockB2);
                load.B2 = hour?.Hours ?? 0;
                load.B2CurriculumLoadSubjectId = pair.B2.Id;
                load.CurriculumLoadSubjectId ??= pair.B2.Id;
                load.IsActive = load.IsActive && pair.B2.IsActive;
            }
            load.Total = SumNullable(load.B1, load.B2);
            subjectDto.GradeLoads.Add(load);
        }

        subjectDto.TotalHours = SumList(subjectDto.GradeLoads.Select(g => g.Total));
        return subjectDto;
    }

    private static CurriculumLoadSubjectRowDto BuildNamedRow(
        string displayName,
        Guid subjectId,
        IEnumerable<LoadRow> cells,
        IReadOnlyList<int> expectedGrades,
        string programName,
        string areaName)
    {
        var cellList = cells.ToList();
        var subjectDto = new CurriculumLoadSubjectRowDto
        {
            SubjectId = subjectId,
            Subject = displayName,
            NeedsReview = !CurriculumLoadDisplayOrder.IsRecognized(programName, areaName, displayName)
        };

        foreach (var grade in expectedGrades)
        {
            var cell = cellList.FirstOrDefault(r =>
                EnrollmentTypeConstants.ParseGradeNumber(r.GradeName) == grade);

            var load = new CurriculumLoadGradeHoursDto { Grade = grade };
            if (cell != null)
            {
                load.CurriculumLoadSubjectId = cell.Id;
                load.B1CurriculumLoadSubjectId = cell.Id;
                load.B2CurriculumLoadSubjectId = cell.Id;
                load.IsActive = cell.IsActive;
                load.B1 = cell.Hours.FirstOrDefault(h => h.Code == CurriculumLoadLevel.BlockB1)?.Hours ?? 0;
                load.B2 = cell.Hours.FirstOrDefault(h => h.Code == CurriculumLoadLevel.BlockB2)?.Hours ?? 0;
                load.Total = SumNullable(load.B1, load.B2);
            }

            subjectDto.GradeLoads.Add(load);
        }

        subjectDto.TotalHours = SumList(subjectDto.GradeLoads.Select(g => g.Total));
        return subjectDto;
    }

    private sealed class LoadRow
    {
        public Guid Id { get; set; }
        public Guid SpecialtyId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public Guid GradeLevelId { get; set; }
        public string GradeName { get; set; } = string.Empty;
        public Guid AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<LoadHour> Hours { get; set; } = new();
    }

    private sealed class LoadHour
    {
        public string Code { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }

    private async Task UpsertOrRemoveHoursAsync(Guid subjectId, Guid blockId, decimal? hours)
    {
        var existing = await _context.CurriculumLoadHours
            .FirstOrDefaultAsync(h => h.CurriculumLoadSubjectId == subjectId && h.CurriculumBlockId == blockId);

        if (!hours.HasValue)
        {
            if (existing != null)
                _context.CurriculumLoadHours.Remove(existing);
            return;
        }

        if (hours.Value < 0)
            throw new InvalidOperationException("Las horas no pueden ser negativas.");

        if (existing == null)
        {
            var created = new CurriculumLoadHours
            {
                Id = Guid.NewGuid(),
                CurriculumLoadSubjectId = subjectId,
                CurriculumBlockId = blockId,
                Hours = hours.Value
            };
            await AuditHelper.SetAuditFieldsForCreateAsync(created, _currentUserService);
            _context.CurriculumLoadHours.Add(created);
        }
        else
        {
            existing.Hours = hours.Value;
            await AuditHelper.SetAuditFieldsForUpdateAsync(existing, _currentUserService);
        }
    }

    private static bool HasCurriculumCell(CurriculumLoadGradeHoursDto load) =>
        load.CurriculumLoadSubjectId.HasValue
        || load.B1CurriculumLoadSubjectId.HasValue
        || load.B2CurriculumLoadSubjectId.HasValue;

    private static decimal? SumNullable(decimal? a, decimal? b)
    {
        if (!a.HasValue && !b.HasValue)
            return null;
        return (a ?? 0) + (b ?? 0);
    }

    private static decimal? SumList(IEnumerable<decimal?> values)
    {
        decimal? total = null;
        foreach (var value in values)
        {
            if (!value.HasValue)
                continue;
            total = (total ?? 0) + value.Value;
        }
        return total;
    }
}
