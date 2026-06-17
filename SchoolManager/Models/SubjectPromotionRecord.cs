namespace SchoolManager.Models;

public partial class SubjectPromotionRecord
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid GradeLevelId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public Guid? TrimesterId { get; set; }

    public Guid? CurriculumSubjectId { get; set; }

    public Guid? AcademicCreditId { get; set; }

    public string Trimester { get; set; } = "";

    /// <summary>Approved, Failed, Pending</summary>
    public string Outcome { get; set; } = "";

    public decimal? FinalScore { get; set; }

    public Guid? StudentSubjectAssignmentId { get; set; }

    public DateTime PromotedAt { get; set; }

    public Guid? SchoolId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual User Student { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;

    public virtual GradeLevel GradeLevel { get; set; } = null!;

    public virtual AcademicYear? AcademicYear { get; set; }

    public virtual Trimester? TrimesterNavigation { get; set; }

    public virtual CurriculumSubject? CurriculumSubject { get; set; }

    public virtual StudentAcademicCredit? AcademicCredit { get; set; }

    public virtual StudentSubjectAssignment? StudentSubjectAssignment { get; set; }

    public virtual School? School { get; set; }
}
