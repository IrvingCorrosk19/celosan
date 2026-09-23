using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class OfficialGradeService : IOfficialGradeService
{
    private readonly SchoolDbContext _context;
    private readonly IEvaluationSchemeResolver _schemeResolver;

    public OfficialGradeService(SchoolDbContext context, IEvaluationSchemeResolver schemeResolver)
    {
        _context = context;
        _schemeResolver = schemeResolver;
    }

    public async Task<OfficialTrimesterGradeResult> GetTrimesterGradeAsync(
        Guid studentSubjectAssignmentId,
        Guid academicYearId,
        Guid trimesterId,
        IEnumerable<(string? Type, decimal? Score)> activityScores)
    {
        if (studentSubjectAssignmentId != Guid.Empty && academicYearId != Guid.Empty && trimesterId != Guid.Empty)
        {
            var imported = await _context.StudentImportedTrimesterGrades.AsNoTracking()
                .FirstOrDefaultAsync(g =>
                    g.StudentSubjectAssignmentId == studentSubjectAssignmentId &&
                    g.AcademicYearId == academicYearId &&
                    g.TrimesterId == trimesterId);
            if (imported != null)
                return ToImported(imported);
        }

        var yearName = await GetAcademicYearNameAsync(academicYearId);
        var trimesterCode = await GetTrimesterCodeAsync(trimesterId);
        return FromActivities(activityScores, yearName, trimesterCode);
    }

    public async Task<OfficialTrimesterGradeResult> GetTrimesterGradeAsync(
        Guid studentSubjectAssignmentId,
        Guid academicYearId,
        string trimesterCode,
        IEnumerable<(string? Type, decimal? Score)> activityScores)
    {
        var code = NormalizeTrimester(trimesterCode);
        if (studentSubjectAssignmentId != Guid.Empty && academicYearId != Guid.Empty && code is "1T" or "2T" or "3T")
        {
            var rows = await GetImportedForSsaYearAsync(studentSubjectAssignmentId, academicYearId);
            var hit = rows.FirstOrDefault(r => r.TrimesterCode == code);
            if (hit != null)
                return ToImported(hit);
        }

        var yearName = await GetAcademicYearNameAsync(academicYearId);
        return FromActivities(activityScores, yearName, code);
    }

    public async Task<IReadOnlyList<ImportedOfficialGradeRow>> GetImportedForStudentAsync(Guid studentId)
    {
        if (studentId == Guid.Empty)
            return Array.Empty<ImportedOfficialGradeRow>();

        var rows = await QueryImportedRows()
            .Where(r => r.StudentId == studentId)
            .ToListAsync();
        return NormalizeCodes(rows);
    }

    public async Task<IReadOnlyList<ImportedOfficialGradeRow>> GetImportedForStudentsAsync(IEnumerable<Guid> studentIds)
    {
        var ids = studentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<ImportedOfficialGradeRow>();

        var rows = await QueryImportedRows()
            .Where(r => ids.Contains(r.StudentId))
            .ToListAsync();
        return NormalizeCodes(rows);
    }

    public OfficialTrimesterGradeResult CalculateFromActivities(
        IEnumerable<(string? Type, decimal? Score)> activityScores,
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?>? contextActivityTypes = null)
    {
        var types = contextActivityTypes ?? activityScores.Select(s => s.Type);
        var scheme = _schemeResolver.ResolveEffective(null, academicYearName, trimesterCode, types);
        var score = OfficialSchemeAverageCalculator.ComputeTrimesterAverage(scheme, activityScores);
        return ToActivitiesResult(score);
    }

    /// <summary>
    /// Imported tiene prioridad. Si no hay importada, calcula con el esquema del año/trimestre.
    /// </summary>
    public static OfficialTrimesterGradeResult SelectOfficial(
        decimal? importedScore,
        Guid? importedGradeId,
        IEnumerable<(string? Type, decimal? Score)> activityScores,
        string? academicYearName,
        string? trimesterCode,
        bool hasImported = false,
        string? importedStatus = null)
    {
        if (hasImported)
        {
            return new OfficialTrimesterGradeResult
            {
                Score = importedScore,
                Status = ImportedTrimesterGradeStatus.Normalize(importedStatus),
                IsImported = true,
                Origin = OfficialTrimesterGradeOrigin.Imported,
                ImportedGradeId = importedGradeId
            };
        }

        return FromActivities(activityScores, academicYearName, trimesterCode);
    }

    public static OfficialTrimesterGradeResult FromActivities(
        IEnumerable<(string? Type, decimal? Score)> activityScores,
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?>? contextActivityTypes = null)
    {
        var types = contextActivityTypes ?? activityScores.Select(s => s.Type);
        var scheme = EvaluationSchemeResolver.ResolveEffective(academicYearName, trimesterCode, types);
        return ToActivitiesResult(OfficialSchemeAverageCalculator.ComputeTrimesterAverage(scheme, activityScores));
    }

    private static OfficialTrimesterGradeResult ToActivitiesResult(decimal? score) =>
        new()
        {
            Score = score,
            IsImported = false,
            Origin = score.HasValue ? OfficialTrimesterGradeOrigin.Activities : OfficialTrimesterGradeOrigin.None
        };

    private IQueryable<ImportedOfficialGradeRow> QueryImportedRows()
    {
        return from g in _context.StudentImportedTrimesterGrades.AsNoTracking()
               join t in _context.Trimesters.AsNoTracking() on g.TrimesterId equals t.Id
               join ssa in _context.StudentSubjectAssignments.AsNoTracking() on g.StudentSubjectAssignmentId equals ssa.Id
               join sa in _context.SubjectAssignments.AsNoTracking() on ssa.SubjectAssignmentId equals sa.Id
               join sub in _context.Subjects.AsNoTracking() on sa.SubjectId equals sub.Id
               select new ImportedOfficialGradeRow
               {
                   ImportedGradeId = g.Id,
                   StudentId = g.StudentId,
                   StudentSubjectAssignmentId = g.StudentSubjectAssignmentId,
                   AcademicYearId = g.AcademicYearId,
                   TrimesterId = g.TrimesterId,
                   SubjectId = sa.SubjectId,
                   SubjectName = sub.Name ?? string.Empty,
                   TrimesterCode = t.Name ?? string.Empty,
                   Score = g.Score,
                   Status = g.Status
               };
    }

    private async Task<List<ImportedOfficialGradeRow>> GetImportedForSsaYearAsync(Guid ssaId, Guid academicYearId)
    {
        var rows = await QueryImportedRows()
            .Where(r => r.StudentSubjectAssignmentId == ssaId && r.AcademicYearId == academicYearId)
            .ToListAsync();
        foreach (var row in rows)
            row.TrimesterCode = NormalizeTrimester(row.TrimesterCode);
        return rows;
    }

    private static IReadOnlyList<ImportedOfficialGradeRow> NormalizeCodes(List<ImportedOfficialGradeRow> rows)
    {
        foreach (var row in rows)
            row.TrimesterCode = NormalizeTrimester(row.TrimesterCode);
        return rows;
    }

    private static OfficialTrimesterGradeResult ToImported(StudentImportedTrimesterGrade imported) =>
        new()
        {
            Score = imported.Score,
            Status = ImportedTrimesterGradeStatus.Normalize(imported.Status),
            IsImported = true,
            Origin = OfficialTrimesterGradeOrigin.Imported,
            ImportedGradeId = imported.Id
        };

    private static OfficialTrimesterGradeResult ToImported(ImportedOfficialGradeRow imported) =>
        new()
        {
            Score = imported.Score,
            Status = ImportedTrimesterGradeStatus.Normalize(imported.Status),
            IsImported = true,
            Origin = OfficialTrimesterGradeOrigin.Imported,
            ImportedGradeId = imported.ImportedGradeId
        };

    private async Task<string?> GetAcademicYearNameAsync(Guid academicYearId)
    {
        if (academicYearId == Guid.Empty)
            return null;

        return await _context.AcademicYears.AsNoTracking()
            .Where(y => y.Id == academicYearId)
            .Select(y => y.Name)
            .FirstOrDefaultAsync();
    }

    private async Task<string?> GetTrimesterCodeAsync(Guid trimesterId)
    {
        if (trimesterId == Guid.Empty)
            return null;

        var name = await _context.Trimesters.AsNoTracking()
            .Where(t => t.Id == trimesterId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync();
        return NormalizeTrimester(name);
    }

    internal static string NormalizeTrimester(string? trimester)
    {
        var t = (trimester ?? string.Empty).Trim().ToUpperInvariant();
        if (t is "T1" or "1T") return "1T";
        if (t is "T2" or "2T") return "2T";
        if (t is "T3" or "3T") return "3T";
        return t;
    }
}
