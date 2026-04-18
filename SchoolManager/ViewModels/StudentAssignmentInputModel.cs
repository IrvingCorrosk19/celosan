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
        /// <summary>Mañana, Tarde o Noche. Vacío en carga masiva se interpreta como Noche (institución nocturna).</summary>
        public string? Jornada { get; set; }
        public bool? Inclusivo { get; set; }  // Inclusivo (true, false, null)

        /// <summary>Regular, Nocturno, Refuerzo, Libre. Opcional; en carga masiva por defecto Nocturno.</summary>
        public string? TipoMatricula { get; set; }
    }
}
