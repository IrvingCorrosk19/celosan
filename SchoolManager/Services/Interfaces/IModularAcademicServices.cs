using SchoolManager.Models;

namespace SchoolManager.Services.Interfaces;

public record PrerequisiteValidationResult(
    bool CanEnroll,
    string Message,
    IReadOnlyList<CurriculumSubject> MissingPrerequisites,
    bool PendingEquivalence);

public record ModularSubjectEnrollmentRequest(
    Guid StudentId,
    Guid SubjectAssignmentId,
    Guid? TrimesterId,
    string? EntryType,
    bool AsCarryOver);

public record ModularSubjectEnrollmentResult(
    bool Handled,
    bool Success,
    string Message,
    Guid? StudentSubjectAssignmentId);

public record ModularPromotionResult(
    bool Success,
    string Message,
    Guid? PromotionRecordId,
    Guid? AcademicCreditId);

public record ModularAcademicAuditResult(
    int StudentsWithoutCurriculum,
    int ActiveSubjectsWithoutTrimester,
    int ActiveSubjectsWithoutCurriculumSubject,
    int CreditsCreated,
    int MissingPrerequisiteRows,
    int BlockedStudents,
    int PendingEquivalencies);

public interface ICurriculumService
{
    Task<IReadOnlyList<CurriculumTrack>> GetTracksAsync(Guid? schoolId = null);
    Task<CurriculumTrack?> GetActiveTrackAsync(Guid? schoolId, Guid? academicYearId = null);
    Task<CurriculumTrack> CreateTrackAsync(CurriculumTrack track);
    Task<CurriculumSubject> AddSubjectAsync(CurriculumSubject subject);
    Task<CurriculumSubjectPrerequisite> AddPrerequisiteAsync(CurriculumSubjectPrerequisite prerequisite);
    Task<CurriculumSubject?> ResolveCurriculumSubjectAsync(Guid schoolId, Guid subjectAssignmentId, Guid? academicYearId = null);
}

public interface IAcademicPrerequisiteService
{
    Task<PrerequisiteValidationResult> ValidateAsync(Guid studentId, Guid curriculumSubjectId);
}

public interface IModularEnrollmentService
{
    Task<bool> IsEnabledForSchoolAsync(Guid? schoolId);
    Task<ModularSubjectEnrollmentResult> EnrollSubjectAsync(ModularSubjectEnrollmentRequest request);
    Task<StudentAcademicPeriodEnrollment?> EnsurePeriodEnrollmentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid trimesterId,
        Guid? studentAssignmentId,
        string? entryType);
}

public interface IAcademicCreditService
{
    Task<StudentAcademicCredit?> GetValidCreditAsync(Guid studentId, Guid curriculumSubjectId);
    Task<StudentAcademicCredit> CreateCreditAsync(StudentAcademicCredit credit);
    Task<StudentAcademicCredit?> CreateFromPromotionAsync(SubjectPromotionRecord promotionRecord);
}

public interface IEquivalencyService
{
    Task<IReadOnlyList<StudentSubjectEquivalency>> GetPendingAsync();
    Task<StudentSubjectEquivalency> CreateAsync(StudentSubjectEquivalency equivalency);
    Task<StudentSubjectEquivalencyItem> AddItemAsync(StudentSubjectEquivalencyItem item);
    Task<StudentAcademicCredit?> ApproveItemAsync(Guid itemId, Guid reviewedBy);
    Task RejectItemAsync(Guid itemId, Guid reviewedBy);
}

public interface IModularPromotionService
{
    Task<ModularPromotionResult> PromoteAsync(
        Guid studentId,
        Guid studentSubjectAssignmentId,
        Guid? trimesterId,
        string trimesterCode,
        decimal? finalScore,
        string? outcome);
}
