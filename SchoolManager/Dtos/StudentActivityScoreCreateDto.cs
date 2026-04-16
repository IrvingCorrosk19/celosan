namespace SchoolManager.Dtos
{
    public class StudentActivityScoreCreateDto
    {
        public Guid StudentId { get; set; }

        /// <summary>Si viene informado, se usa directamente; si no, se infiere por actividad o por grupo+grado.</summary>
        public Guid? StudentAssignmentId { get; set; }

        public Guid? StudentSubjectAssignmentId { get; set; }

        public Guid ActivityId { get; set; }
        public string ActivityName { get; set; }  
        public string Type { get; set; }         
        public Guid GroupId { get; set; }        
        public Guid SubjectId { get; set; }
        public Guid TeacherId { get; set; }       
        public Guid GradeLevelId { get; set; }
        public string Trimester { get; set; }    
        public decimal? Score { get; set; }       

    }
}
