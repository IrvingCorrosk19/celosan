public class GroupGradeItem
{
    public Guid GroupId { get; set; }
    public Guid GradeLevelId { get; set; }
    public Guid? ShiftId { get; set; }
    public string GroupName { get; set; } = "";
    public string GradeLevelName { get; set; } = "";
    public string SpecialtyName { get; set; } = "";
    public string AreaName { get; set; } = "";
}
