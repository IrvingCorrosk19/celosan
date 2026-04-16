namespace SchoolManager.Dtos
{
    public class EstadisticasFiltroDto
    {
        public Guid GroupId { get; set; }
        public Guid GradeId { get; set; }
        public Guid? ShiftId { get; set; }
        public string Trimestre { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
    }
} 