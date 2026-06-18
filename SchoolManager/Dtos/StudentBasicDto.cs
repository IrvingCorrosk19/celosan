namespace SchoolManager.Dtos
{
    public class StudentBasicDto
    {
        public Guid StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string GradeName { get; set; } = "";
        public string GroupName { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public Guid? StudentSubjectAssignmentId { get; set; }
    }
}
