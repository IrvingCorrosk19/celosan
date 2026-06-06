namespace SchoolManager.Services.Interfaces;

public interface ISubjectPromotionService
{
    Task<(bool Success, string Message)> PromoteSubjectAsync(
        Guid studentId,
        Guid studentSubjectAssignmentId,
        string trimester,
        decimal? finalScore,
        string outcome);

    Task<(bool Success, string Message, int Processed)> CloseYearForStudentAsync(
        Guid studentId,
        string trimester,
        decimal passingScore = 3.0m);

    Task<IReadOnlyList<SubjectPromotionRecordDto>> GetRecordsByStudentAsync(Guid studentId, Guid? academicYearId = null);
}

public class SubjectPromotionRecordDto
{
    public Guid Id { get; set; }
    public string SubjectName { get; set; } = "";
    public string GradeName { get; set; } = "";
    public string Trimester { get; set; } = "";
    public string Outcome { get; set; } = "";
    public decimal? FinalScore { get; set; }
    public DateTime PromotedAt { get; set; }
}
