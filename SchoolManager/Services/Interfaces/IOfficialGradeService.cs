using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolManager.Dtos;
using SchoolManager.Helpers;

namespace SchoolManager.Services.Interfaces;

public interface IOfficialGradeService
{
    Task<OfficialTrimesterGradeResult> GetTrimesterGradeAsync(
        Guid studentSubjectAssignmentId,
        Guid academicYearId,
        Guid trimesterId,
        IEnumerable<(string? Type, decimal? Score)> activityScores);

    Task<OfficialTrimesterGradeResult> GetTrimesterGradeAsync(
        Guid studentSubjectAssignmentId,
        Guid academicYearId,
        string trimesterCode,
        IEnumerable<(string? Type, decimal? Score)> activityScores);

    Task<IReadOnlyList<ImportedOfficialGradeRow>> GetImportedForStudentAsync(Guid studentId);

    Task<IReadOnlyList<ImportedOfficialGradeRow>> GetImportedForStudentsAsync(IEnumerable<Guid> studentIds);

    /// <summary>
    /// Fallback de activities según el esquema del año/trimestre. No consulta importadas.
    /// </summary>
    OfficialTrimesterGradeResult CalculateFromActivities(
        IEnumerable<(string? Type, decimal? Score)> activityScores,
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?>? contextActivityTypes = null);
}

public class ImportedOfficialGradeRow
{
    public Guid ImportedGradeId { get; set; }
    public Guid StudentId { get; set; }
    public Guid StudentSubjectAssignmentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid TrimesterId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string TrimesterCode { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string Status { get; set; } = ImportedTrimesterGradeStatus.Graded;
}
