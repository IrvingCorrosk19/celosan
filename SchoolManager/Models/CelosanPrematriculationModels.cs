namespace SchoolManager.Models;

public partial class StudentPrematriculationSubjectSelection
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid PrematriculationId { get; set; }
    public Guid PrematriculationPeriodId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CurriculumSubjectId { get; set; }
    public Guid? SubjectAssignmentId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? TeacherAssignmentId { get; set; }
    public string Status { get; set; } = "Draft";
    public string ValidationStatus { get; set; } = "Pending";
    public string? ValidationMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Prematriculation Prematriculation { get; set; } = null!;
    public virtual PrematriculationPeriod PrematriculationPeriod { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual CurriculumSubject CurriculumSubject { get; set; } = null!;
    public virtual SubjectAssignment? SubjectAssignment { get; set; }
    public virtual Group? Group { get; set; }
    public virtual TeacherAssignment? TeacherAssignment { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual User? UpdatedByUser { get; set; }
}

public partial class PrematriculationReceipt
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid PrematriculationId { get; set; }
    public Guid PrematriculationPeriodId { get; set; }
    public Guid StudentId { get; set; }
    public string Consecutive { get; set; } = null!;
    public int Version { get; set; }
    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedBy { get; set; }
    public string? PdfUrl { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Prematriculation Prematriculation { get; set; } = null!;
    public virtual PrematriculationPeriod PrematriculationPeriod { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual User? GeneratedByUser { get; set; }
}

public partial class PrematriculationReopenAuthorization
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid PrematriculationId { get; set; }
    public Guid PrematriculationPeriodId { get; set; }
    public Guid StudentId { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime AuthorizedAt { get; set; }
    public Guid AuthorizedBy { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Prematriculation Prematriculation { get; set; } = null!;
    public virtual PrematriculationPeriod PrematriculationPeriod { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual User AuthorizedByUser { get; set; } = null!;
}

public partial class StudentSubjectWithdrawalRequest
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentSubjectAssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectAssignmentId { get; set; }
    public Guid RequestedBy { get; set; }
    public string Reason { get; set; } = null!;
    public string? Observation { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewObservation { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual StudentSubjectAssignment StudentSubjectAssignment { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual SubjectAssignment SubjectAssignment { get; set; } = null!;
    public virtual User RequestedByUser { get; set; } = null!;
    public virtual User? ReviewedByUser { get; set; }
}
