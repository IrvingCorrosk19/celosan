using System;
using System.Collections.Generic;

namespace SchoolManager.Models;

public partial class Attendance
{
    public Guid Id { get; set; }

    public Guid? SchoolId { get; set; }

    public Guid? StudentId { get; set; }

    public Guid? TeacherId { get; set; }

    public Guid? GroupId { get; set; }

    public Guid? GradeId { get; set; }

    public Guid? ShiftId { get; set; }

    public Guid? StudentAssignmentId { get; set; }

    public DateOnly Date { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual GradeLevel? Grade { get; set; }

    public virtual Group? Group { get; set; }

    public virtual School? School { get; set; }

    public virtual Shift? Shift { get; set; }

    public virtual User? Student { get; set; }

    public virtual StudentAssignment? StudentAssignment { get; set; }

    public virtual User? Teacher { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual User? UpdatedByUser { get; set; }
}
