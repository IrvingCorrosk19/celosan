using SchoolManager.Dtos;

namespace SchoolManager.Services.Interfaces;

public interface ICurriculumLoadService
{
    Task<List<AcademicProgramOptionDto>> GetProgramsForTeacherAsync(Guid teacherId);
    Task<List<AcademicProgramOptionDto>> GetProgramsForAdministrationAsync();
    Task<CurriculumLoadReportDto?> GetReportAsync(Guid programId, string viewMode, int? selectedGrade, bool includeInactive);
    Task<List<CurriculumLoadInactiveRowDto>> GetInactiveSubjectsAsync(Guid programId);
    Task SaveHoursAsync(Guid curriculumLoadSubjectId, decimal? b1, decimal? b2, string? onlyBlock = null);
    Task SetActiveAsync(Guid curriculumLoadSubjectId, bool isActive);
}
