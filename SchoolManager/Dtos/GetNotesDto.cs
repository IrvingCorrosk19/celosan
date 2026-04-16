namespace SchoolManager.Dtos
{
    public class GetNotesDto
    {
        public Guid TeacherId { get; set; }
        public Guid SubjectId { get; set; }
        public Guid GroupId { get; set; }
        public Guid GradeLevelId { get; set; }
        public Guid? ShiftId { get; set; }
        public string Trimester { get; set; }
    }
}
