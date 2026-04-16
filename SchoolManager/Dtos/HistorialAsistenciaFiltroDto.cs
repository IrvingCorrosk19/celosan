namespace SchoolManager.Dtos
{
    public class HistorialAsistenciaFiltroDto
    {
        public Guid GroupId { get; set; }
        public Guid GradeId { get; set; }
        public Guid? ShiftId { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
        public string? StudentId { get; set; }
    }
} 