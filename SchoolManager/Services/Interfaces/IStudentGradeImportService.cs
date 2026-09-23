using Microsoft.AspNetCore.Http;
using SchoolManager.Dtos;

namespace SchoolManager.Services.Interfaces;

public interface IStudentGradeImportService
{
    Task<GradeImportPageDto> GetPageAsync(Guid? schoolId);
    Task<GradeImportAnalyzeResult> AnalyzeAsync(Guid userId, Guid schoolId, Guid academicYearId, IFormFile file);
    Task<GradeImportConfirmResult> ConfirmAsync(Guid userId, Guid schoolId, string previewToken, string? userName, string? userRole);
}
