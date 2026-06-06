using System.ComponentModel.DataAnnotations;

namespace SchoolManager.ViewModels
{
    /// <summary>
    /// ViewModel para el reporte de aprobados y reprobados por grado
    /// </summary>
    public class AprobadosReprobadosReportViewModel
    {
        public string InstitutoNombre { get; set; } = null!;
        public string LogoUrl { get; set; } = null!;
        public string ProfesorCoordinador { get; set; } = null!;
        public string Trimestre { get; set; } = null!;
        public string AnoLectivo { get; set; } = null!;
        public string NivelEducativo { get; set; } = null!; // "Premedia" o "Media"
        public DateTime FechaGeneracion { get; set; }
        
        public List<GradoEstadisticaDto> Estadisticas { get; set; } = new();
        public TotalesGeneralesDto TotalesGenerales { get; set; } = new();
        
        // Para filtros
        public List<string> TrimestresDisponibles { get; set; } = new();
        public List<string> NivelesDisponibles { get; set; } = new();
    }

    /// <summary>
    /// Estadísticas por grado y grupo
    /// </summary>
    public class GradoEstadisticaDto
    {
        public string Grado { get; set; } = null!;
        public string Grupo { get; set; } = null!;
        public int TotalEstudiantes { get; set; }
        
        // Aprobados
        public int Aprobados { get; set; }
        public decimal PorcentajeAprobados { get; set; }
        
        // Reprobados
        public int Reprobados { get; set; }
        public decimal PorcentajeReprobados { get; set; }
        
        // Reprobados hasta la fecha
        public int ReprobadosHastaLaFecha { get; set; }
        public decimal PorcentajeReprobadosHastaLaFecha { get; set; }
        
        // Sin calificaciones
        public int SinCalificaciones { get; set; }
        public decimal PorcentajeSinCalificaciones { get; set; }
        
        // Retirados
        public int Retirados { get; set; }
        public decimal PorcentajeRetirados { get; set; }
    }

    /// <summary>
    /// Totales generales del reporte
    /// </summary>
    public class TotalesGeneralesDto
    {
        public int TotalEstudiantes { get; set; }
        public int TotalAprobados { get; set; }
        public decimal PorcentajeAprobados { get; set; }
        public int TotalReprobados { get; set; }
        public decimal PorcentajeReprobados { get; set; }
        public int TotalReprobadosHastaLaFecha { get; set; }
        public decimal PorcentajeReprobadosHastaLaFecha { get; set; }
        public int TotalSinCalificaciones { get; set; }
        public decimal PorcentajeSinCalificaciones { get; set; }
        public int TotalRetirados { get; set; }
        public decimal PorcentajeRetirados { get; set; }
    }

    /// <summary>
    /// ViewModel para los filtros del reporte
    /// </summary>
    public class AprobadosReprobadosFiltroViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un trimestre")]
        public string Trimestre { get; set; } = null!;

        [Required(ErrorMessage = "Debe seleccionar un nivel educativo")]
        public string NivelEducativo { get; set; } = null!; // "Premedia" o "Media"

        public string? GradoEspecifico { get; set; }
        public string? GrupoEspecifico { get; set; }

        /// <summary>Evita doble conteo cuando un estudiante aparece en varios grupos (modo nocturno).</summary>
        public bool ConsolidadoPorEstudiante { get; set; }
        
        // Nuevos filtros
        public Guid? EspecialidadId { get; set; }
        public Guid? AreaId { get; set; }
        public Guid? MateriaId { get; set; }
    }
}

