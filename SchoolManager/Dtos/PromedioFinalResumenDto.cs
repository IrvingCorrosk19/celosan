namespace SchoolManager.Dtos
{
    public class PromedioFinalResumenDto
    {
        public string StudentId { get; set; }
        public string StudentFullName { get; set; }
        public string DocumentId { get; set; }
        public decimal? T1 { get; set; }
        public decimal? T2 { get; set; }
        public decimal? T3 { get; set; }
        public decimal? PromedioFinal { get; set; }
        public string Estado { get; set; }
    }
}
