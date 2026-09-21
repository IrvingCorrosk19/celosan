namespace SchoolManager.Dtos;

public class StudentProgramHistoryDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public List<ProgramHistoryTrackDto> Tracks { get; set; } = new();
}

public class ProgramHistoryTrackDto
{
    public Guid SpecialtyId { get; set; }
    public string ProgramType { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public List<ProgramHistoryGradeColumnDto> Grades { get; set; } = new();
    public List<ProgramHistoryAreaDto> Areas { get; set; } = new();
}

public class ProgramHistoryGradeColumnDto
{
    public Guid? GradeId { get; set; }
    public int GradeNumber { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = "—";
}

public class ProgramHistoryAreaDto
{
    public Guid AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public List<ProgramHistorySubjectDto> Subjects { get; set; } = new();
}

public class ProgramHistorySubjectDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public List<ProgramHistoryGradeResultDto> GradeResults { get; set; } = new();
}

public class ProgramHistoryGradeResultDto
{
    public int GradeNumber { get; set; }
    public Guid? GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string? AcademicYear { get; set; }
    public decimal? FinalAverage { get; set; }
}
