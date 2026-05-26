using System.Collections.Generic;

namespace SchoolManager.ViewModels
{
    public class StudentAssignmentIndexViewModel
    {
        public List<StudentAssignmentOverviewViewModel> Students { get; set; } = new();
        public List<string> GradeFilterOptions { get; set; } = new();
        public List<string> GroupFilterOptions { get; set; } = new();
        public List<string> ShiftFilterOptions { get; set; } = new();
    }

    /// <summary>Datos de matrícula activa para filtrar filas en Index.</summary>
    public class StudentEnrollmentFilterItem
    {
        public string Grade { get; set; } = "";
        public string Group { get; set; } = "";
        public string Shift { get; set; } = "";
    }
}
