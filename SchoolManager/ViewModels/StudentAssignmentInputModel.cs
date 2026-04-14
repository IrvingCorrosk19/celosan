namespace SchoolManager.ViewModels
{
    public class StudentAssignmentInputModel
    {
        public string Estudiante { get; set; } = string.Empty; // Email
        public string Nombre { get; set; } = string.Empty;     // Nombre del estudiante
        public string Apellido { get; set; } = string.Empty;   // Apellido del estudiante
        public string DocumentoId { get; set; } = string.Empty; // Documento de identidad
        public string FechaNacimiento { get; set; } = string.Empty; // Fecha de nacimiento
        public string Grado { get; set; } = string.Empty;      // Nombre del grado
        public string Grupo { get; set; } = string.Empty;      // Nombre del grupo
        public string? Jornada { get; set; }  // Jornada: Mañana, Tarde, Noche
        public bool? Inclusivo { get; set; }  // Inclusivo (true, false, null)

        /// <summary>Tipo de matrícula (Regular, Nocturno, Refuerzo, Libre). Opcional; por defecto Regular.</summary>
        public string? TipoMatricula { get; set; }
    }
}
