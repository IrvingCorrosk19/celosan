namespace SchoolManager.Dtos
{
    public class GradeDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal? Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? FileUrl { get; set; }
        public string Trimester { get; set; } = string.Empty;
        /// <summary>Grupo u oferta académica (evita ambigüedad con varias matrículas).</summary>
        public string? GroupContext { get; set; }
        public decimal? Grade { get { return Value; } }
    }

}
