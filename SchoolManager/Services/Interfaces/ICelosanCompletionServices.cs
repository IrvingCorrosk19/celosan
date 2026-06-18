using Microsoft.AspNetCore.Http;
using SchoolManager.Models;

namespace SchoolManager.Services.Interfaces;

public record CelosanBulkImportResult(
    bool Success,
    int ProcessedRows,
    int SuccessRows,
    int ErrorRows,
    IReadOnlyList<string> Errors);

public record CelosanReportRow(string Label, string Value, string? Detail = null);

public record CelosanReportDashboardDto(
    IReadOnlyList<CelosanReportRow> PrematriculatedStudents,
    IReadOnlyList<CelosanReportRow> SubjectDemand,
    IReadOnlyList<CelosanReportRow> AvailableSeats,
    IReadOnlyList<CelosanReportRow> FullGroups,
    IReadOnlyList<CelosanReportRow> StudentsByTeacher,
    IReadOnlyList<CelosanReportRow> ExpiredDocuments,
    IReadOnlyList<CelosanReportRow> WithdrawnSubjects,
    IReadOnlyList<CelosanReportRow> AcademicProgress,
    IReadOnlyList<CelosanReportRow> ChangeHistory);

public interface IStudentIdentityDocumentService
{
    Task<StudentIdentityDocument?> GetLatestAsync(Guid studentId);
    Task<StudentIdentityDocument> SaveAsync(Guid studentId, string? documentNumber, DateTime? expirationDate, IFormFile file);
    Task<IReadOnlyList<StudentIdentityDocument>> GetExpiredOrMissingAsync(Guid? schoolId = null);
}

public interface ICelosanBulkImportService
{
    Task<CelosanBulkImportResult> ImportApprovedCreditsAsync(IFormFile file);
}

public interface ICelosanReportService
{
    Task<CelosanReportDashboardDto> BuildDashboardAsync(Guid? schoolId = null);
}
