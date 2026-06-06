namespace SchoolManager.Services.Interfaces;

public class SubjectCatalogItemDto
{
    public Guid SubjectAssignmentId { get; set; }
    public string SubjectName { get; set; } = "";
    public string GradeName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Display { get; set; } = "";
    public bool IsCarryOverGrade { get; set; }
    public bool RequiresNewEnrollment { get; set; }
}
