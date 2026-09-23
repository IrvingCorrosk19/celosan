namespace SchoolManager.Dtos;

public class StudentBulletinDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public List<AreaBulletinDto> Areas { get; set; } = new();
    public List<PendingPremediaSubjectDto> PendingPremedia { get; set; } = new();
}

public class PendingPremediaSubjectDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int GradeNumber { get; set; }
    public Guid GradeLevelId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public decimal? T1 { get; set; }
    public decimal? T2 { get; set; }
    public decimal? T3 { get; set; }
    public string T1Display { get; set; } = "—";
    public string T2Display { get; set; } = "—";
    public string T3Display { get; set; } = "—";
    public decimal? FinalAverage { get; set; }
}

public class AreaBulletinDto
{
    public Guid AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public List<SubjectBulletinDto> Subjects { get; set; } = new();
}

public class SubjectBulletinDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public decimal? T1 { get; set; }
    public decimal? T2 { get; set; }
    public decimal? T3 { get; set; }
    public string T1Display { get; set; } = "—";
    public string T2Display { get; set; } = "—";
    public string T3Display { get; set; } = "—";
    public decimal? FinalAverage { get; set; }
}
