using System;

namespace SchoolManager.Models;

public partial class StudentSubjectAssignment
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid SubjectAssignmentId { get; set; }

    public Guid? StudentAssignmentId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public Guid? ShiftId { get; set; }

    public string EnrollmentType { get; set; } = "Regular";

    public string Status { get; set; } = "Active";

    public bool IsActive { get; set; } = true;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public Guid? SchoolId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual User Student { get; set; } = null!;

    public virtual SubjectAssignment SubjectAssignment { get; set; } = null!;

    public virtual StudentAssignment? StudentAssignment { get; set; }

    public virtual AcademicYear? AcademicYear { get; set; }

    public virtual Shift? Shift { get; set; }

    public virtual School? School { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual User? UpdatedByUser { get; set; }
}
