namespace SchoolManager.Dtos;

public class StudentEnrollmentSummaryDto
{
    public string GradeName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string? ShiftName { get; set; }
    public string EnrollmentType { get; set; } = "";
    public bool IsPrimary { get; set; }
    public bool IsCarryOver { get; set; }
}
