namespace SchoolManager.Dtos
{
    public class SubjectTrimesterAverageDto
    {
        public string Subject { get; set; } = string.Empty;
        public decimal? AverageApreciacion { get; set; }
        public decimal? AverageEjercicios { get; set; }
        public decimal? AverageExamen { get; set; }
        public decimal? SubjectAverage { get; set; }
    }
}
