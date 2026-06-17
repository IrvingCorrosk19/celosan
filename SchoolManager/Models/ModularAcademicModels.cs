namespace SchoolManager.Models;

public partial class CurriculumTrack
{
    public Guid Id { get; set; }
    public Guid? SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? AcademicYearId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual School? School { get; set; }
    public virtual AcademicYear? AcademicYear { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual User? UpdatedByUser { get; set; }
    public virtual ICollection<CurriculumSubject> CurriculumSubjects { get; set; } = new List<CurriculumSubject>();
}

public partial class CurriculumSubject
{
    public Guid Id { get; set; }
    public Guid CurriculumTrackId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? GradeLevelId { get; set; }
    public string LevelName { get; set; } = null!;
    public int ModuleOrder { get; set; }
    public decimal Credits { get; set; }
    public decimal MinimumPassingScore { get; set; } = 3.0m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual CurriculumTrack CurriculumTrack { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual GradeLevel? GradeLevel { get; set; }
}

public partial class CurriculumSubjectPrerequisite
{
    public Guid Id { get; set; }
    public Guid CurriculumSubjectId { get; set; }
    public Guid PrerequisiteCurriculumSubjectId { get; set; }
    public string RequirementType { get; set; } = "Required";
    public decimal? MinimumScore { get; set; }
    public bool AllowEquivalence { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual CurriculumSubject CurriculumSubject { get; set; } = null!;
    public virtual CurriculumSubject PrerequisiteCurriculumSubject { get; set; } = null!;
}

public partial class StudentAcademicPeriodEnrollment
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid TrimesterId { get; set; }
    public Guid? StudentAssignmentId { get; set; }
    public string EntryType { get; set; } = "Regular";
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual AcademicYear AcademicYear { get; set; } = null!;
    public virtual Trimester Trimester { get; set; } = null!;
    public virtual StudentAssignment? StudentAssignment { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual User? UpdatedByUser { get; set; }
}

public partial class StudentAcademicCredit
{
    public Guid Id { get; set; }
    public Guid? SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CurriculumSubjectId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? GradeLevelId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? TrimesterId { get; set; }
    public string SourceType { get; set; } = null!;
    public Guid? SourceId { get; set; }
    public decimal? FinalScore { get; set; }
    public DateTime ApprovedAt { get; set; }
    public string Status { get; set; } = "Valid";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public virtual School? School { get; set; }
    public virtual User Student { get; set; } = null!;
    public virtual CurriculumSubject CurriculumSubject { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual GradeLevel? GradeLevel { get; set; }
    public virtual AcademicYear? AcademicYear { get; set; }
    public virtual Trimester? Trimester { get; set; }
    public virtual User? CreatedByUser { get; set; }
}

public partial class StudentSubjectEquivalency
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public string SourceInstitutionName { get; set; } = null!;
    public string? SourceCountry { get; set; }
    public string? CertificateNumber { get; set; }
    public string? DocumentUrl { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual User? ReviewedByUser { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<StudentSubjectEquivalencyItem> Items { get; set; } = new List<StudentSubjectEquivalencyItem>();
}

public partial class StudentSubjectEquivalencyItem
{
    public Guid Id { get; set; }
    public Guid EquivalencyId { get; set; }
    public Guid CurriculumSubjectId { get; set; }
    public string ExternalSubjectName { get; set; } = null!;
    public string? ExternalScore { get; set; }
    public decimal? NormalizedScore { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual StudentSubjectEquivalency Equivalency { get; set; } = null!;
    public virtual CurriculumSubject CurriculumSubject { get; set; } = null!;
}
