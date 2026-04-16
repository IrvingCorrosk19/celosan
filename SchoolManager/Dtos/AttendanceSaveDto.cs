using System;

namespace SchoolManager.Dtos
{
    public class AttendanceSaveDto
    {
        public Guid StudentId { get; set; }
        public Guid TeacherId { get; set; }
        public Guid GroupId { get; set; }
        public Guid GradeId { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? StudentAssignmentId { get; set; }
        public DateOnly Date { get; set; }
        public string Status { get; set; } = string.Empty; // "present", "absent", "late", "fuga", "excusa"
    }
} 