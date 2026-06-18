namespace SchoolManager.ViewModels
{
    public class StudentSubjectEnrollmentInputModel
    {
        // Identificación del estudiante (correo).
        public string EstudianteEmail { get; set; } = string.Empty;

        // Opcional: usado para crear/actualizar el usuario si no existe.
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? DocumentoId { get; set; }

        // Catálogo: la materia a matricular (por nombre o código).
        public string Asignatura { get; set; } = string.Empty;
        
        // Catálogo académico.
        public string Nivel { get; set; } = string.Empty;
        public string GrupoAcademico { get; set; } = string.Empty;

        // Jornada opcional (Mañana/Tarde/Noche).
        public string? Jornada { get; set; }

        // Jornada/Tipo inscripcion opcional (Refuerzo, Libre, Nocturno).
        public string? TipoInscripcion { get; set; }

        // Modelo modular: código/nombre de trimestre (1T/2T/3T) o ID en texto.
        public string? Trimestre { get; set; }

        // Modelo modular: Regular, LateEntry, Transfer, Reentry.
        public string? TipoIngreso { get; set; }

        // Si viene false, se desactiva (IsActive=false) la inscripción para esa materia en el año académico.
        public bool Inscrito { get; set; } = true;
    }
}

