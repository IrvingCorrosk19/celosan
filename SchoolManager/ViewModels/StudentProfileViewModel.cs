using System.ComponentModel.DataAnnotations;
using SchoolManager.Dtos;

namespace SchoolManager.ViewModels
{
    /// <summary>
    /// ViewModel para que los estudiantes editen su perfil personal
    /// </summary>
    public class StudentProfileViewModel
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres")]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(100, ErrorMessage = "El apellido no puede tener más de 100 caracteres")]
        [Display(Name = "Apellido")]
        public string LastName { get; set; } = null!;

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [StringLength(100, ErrorMessage = "El correo no puede tener más de 100 caracteres")]
        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; } = null!;

        [StringLength(50, ErrorMessage = "El documento de identidad no puede tener más de 50 caracteres")]
        [Display(Name = "Cédula / Documento de Identidad")]
        public string? DocumentId { get; set; }

        [Display(Name = "Fecha de Nacimiento")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Phone(ErrorMessage = "El formato del teléfono no es válido")]
        [StringLength(20, ErrorMessage = "El teléfono no puede tener más de 20 caracteres")]
        [Display(Name = "Teléfono Principal")]
        public string? CellphonePrimary { get; set; }

        [Phone(ErrorMessage = "El formato del teléfono no es válido")]
        [StringLength(20, ErrorMessage = "El teléfono no puede tener más de 20 caracteres")]
        [Display(Name = "Teléfono Secundario")]
        public string? CellphoneSecondary { get; set; }

        // Información de solo lectura (no editable por el estudiante)
        [Display(Name = "Grado")]
        public string? Grade { get; set; }

        [Display(Name = "Grupo")]
        public string? GroupName { get; set; }

        [Display(Name = "Otras matrículas")]
        public string? AdditionalEnrollmentsSummary { get; set; }

        public List<StudentEnrollmentSummaryDto> ActiveEnrollments { get; set; } = new();

        [Display(Name = "Escuela")]
        public string? SchoolName { get; set; }

        public string? Role { get; set; }

        /// <summary>URL o path de la foto del estudiante (solo lectura desde vista).</summary>
        [Display(Name = "Foto")]
        public string? PhotoUrl { get; set; }

        [Display(Name = "Contacto de emergencia")]
        public string? EmergencyContactName { get; set; }
        [Display(Name = "Teléfono emergencia")]
        public string? EmergencyContactPhone { get; set; }
        [Display(Name = "Relación")]
        public string? EmergencyRelationship { get; set; }
        [Display(Name = "Tipo de sangre")]
        [StringLength(10)]
        public string? BloodType { get; set; }

        [Display(Name = "Alergias")]
        public string? Allergies { get; set; }
        /// <summary>Si true, la vista puede mostrar la sección de información de emergencia (solo para roles inspector/teacher/admin).</summary>
        public bool ShowEmergencyInfo { get; set; }
    }
}

