namespace SchoolManager.Dtos
{
    public class StudentReportDto
    {
        public Guid StudentId { get; set; }
        public string StudentName { get; set; }
        public string Grade { get; set; }
        public string Trimester { get; set; }
        public List<GradeDto> Grades { get; set; } = new();
        public List<GradeDto> CarryOverGrades { get; set; } = new();
        public List<StudentEnrollmentSummaryDto> ActiveEnrollments { get; set; } = new();
        public List<AttendanceDto> AttendanceByTrimester { get; set; } = new();
        public List<AttendanceDto> AttendanceByMonth { get; set; } = new();
        public List<AvailableTrimesters> AvailableTrimesters { get; set; } = new();
        public List<DisciplineReportDto> DisciplineReports { get; set; } = new();
        public List<PendingActivityDto> PendingActivities { get; set; } = new();
        public List<string> AvailableSubjects { get; set; } = new();
    }

    public class AvailableTrimesters
    {
        public string Trimester { get; set; }   
    }

    public class PendingActivityDto
    {
        public Guid ActivityId { get; set; }
        public string Name { get; set; }
        public string SubjectName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? FileUrl { get; set; }
        public string TeacherName { get; set; }
        public string Type { get; set; }
    }
}
