namespace SchoolManager.ViewModels
{
    public class StudentAssignmentOverviewViewModel
    {
        public Guid StudentId { get; set; }
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DocumentId { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public List<string> GradeGroupPairs { get; set; } = new();
        /// <summary>Matrículas activas desglosadas (grado, grupo, jornada) para filtros en Index.</summary>
        public List<StudentEnrollmentFilterItem> Enrollments { get; set; } = new();
    }


    public class StudentSubjectSummary
    {
        public string SubjectName { get; set; }
        public List<string> GradeGroupPairs { get; set; } = new();
    }

}
