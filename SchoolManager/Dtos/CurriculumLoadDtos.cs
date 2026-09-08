namespace SchoolManager.Dtos;

public class AcademicProgramOptionDto
{
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProgramType { get; set; } = string.Empty;
}

public class CurriculumLoadReportDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramType { get; set; } = string.Empty;
    public string HeaderTitle { get; set; } = string.Empty;
    public string ViewMode { get; set; } = string.Empty;
    public int? SelectedGrade { get; set; }
    public bool CanEdit { get; set; }

    public List<int> Grades { get; set; } = new();
    public List<CurriculumLoadAreaDto> Areas { get; set; } = new();

    public List<CurriculumLoadGradeTotalDto> TotalHoursByGrade { get; set; } = new();
    public decimal? TotalHours { get; set; }

    public List<CurriculumLoadSubjectCountDto> SubjectCountsByGrade { get; set; } = new();
    public int TotalSubjects { get; set; }
    public List<CurriculumLoadInactiveRowDto> InactiveSubjects { get; set; } = new();
}

public class CurriculumLoadAreaDto
{
    public Guid AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CurriculumLoadSubjectRowDto> Subjects { get; set; } = new();
    public List<CurriculumLoadGradeTotalDto> TotalsByGrade { get; set; } = new();
    public decimal? TotalHours { get; set; }
}

public class CurriculumLoadSubjectRowDto
{
    public Guid SubjectId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
    public List<CurriculumLoadGradeHoursDto> GradeLoads { get; set; } = new();
    public decimal? TotalHours { get; set; }
}

public class CurriculumLoadGradeHoursDto
{
    public int Grade { get; set; }
    public Guid? CurriculumLoadSubjectId { get; set; }
    public Guid? B1CurriculumLoadSubjectId { get; set; }
    public Guid? B2CurriculumLoadSubjectId { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? B1 { get; set; }
    public decimal? B2 { get; set; }
    public decimal? Total { get; set; }
}

public class CurriculumLoadInactiveRowDto
{
    public Guid CurriculumLoadSubjectId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
}

public class CurriculumLoadGradeTotalDto
{
    public int Grade { get; set; }
    public decimal? B1 { get; set; }
    public decimal? B2 { get; set; }
    public decimal? Total { get; set; }
}

public class CurriculumLoadSubjectCountDto
{
    public int Grade { get; set; }
    public int B1 { get; set; }
    public int B2 { get; set; }
    public int InGrade { get; set; }
}

public class CurriculumLoadSaveHoursRequest
{
    public Guid CurriculumLoadSubjectId { get; set; }
    public decimal? B1 { get; set; }
    public decimal? B2 { get; set; }
    public string? OnlyBlock { get; set; }
}

public class CurriculumLoadSetActiveRequest
{
    public Guid CurriculumLoadSubjectId { get; set; }
    public bool IsActive { get; set; }
}
