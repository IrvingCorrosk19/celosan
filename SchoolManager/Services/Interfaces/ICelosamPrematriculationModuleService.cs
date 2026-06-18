using SchoolManager.Models;

namespace SchoolManager.Services.Interfaces;

public record CelosamAvailableSubjectDto(
    Guid CurriculumSubjectId,
    string SubjectName,
    string GradeName,
    int ModuleOrder,
    decimal Credits,
    bool IsApproved,
    bool IsFailedOrWithdrawn,
    bool CanSelect,
    string Status,
    string Message,
    int AvailableGroups);

public record CelosamSelectedSubjectDto(
    Guid SelectionId,
    Guid CurriculumSubjectId,
    string SubjectName,
    string GradeName,
    string Status,
    string? GroupName,
    string? TeacherName,
    string ScheduleText,
    string? ValidationMessage);

public record CelosamAcademicProgressDto(
    int TotalSubjects,
    int ApprovedSubjects,
    int PendingSubjects,
    int FailedOrWithdrawnSubjects,
    decimal ProgressPercent);

public record CelosamPrematriculationDashboardDto(
    Prematriculation Prematriculation,
    PrematriculationPeriod Period,
    IReadOnlyList<CelosamAvailableSubjectDto> AvailableSubjects,
    IReadOnlyList<CelosamSelectedSubjectDto> SelectedSubjects,
    CelosamAcademicProgressDto Progress);

public record CelosamFinalizeResult(
    bool Success,
    string Message,
    Guid? ReceiptId);

public interface ICelosamPrematriculationModuleService
{
    Task<CelosamPrematriculationDashboardDto?> GetDashboardAsync(Guid prematriculationId);
    Task<(bool Success, string Message)> SelectSubjectAsync(Guid prematriculationId, Guid curriculumSubjectId);
    Task<(bool Success, string Message)> RemoveSubjectAsync(Guid selectionId);
    Task<CelosamFinalizeResult> FinalizeAsync(Guid prematriculationId);
    Task<(bool Success, string Message)> ReopenAsync(Guid prematriculationId, string reason);
    Task<byte[]?> GenerateReceiptPdfAsync(Guid receiptId);
}

public interface ISubjectWithdrawalRequestService
{
    Task<(bool Success, string Message)> RequestAsync(Guid studentSubjectAssignmentId, string reason, string? observation);
    Task<IReadOnlyList<StudentSubjectWithdrawalRequest>> GetPendingAsync();
    Task<(bool Success, string Message)> ReviewAsync(Guid requestId, bool approve, string? reviewObservation);
}
