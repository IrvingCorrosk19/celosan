using System.ComponentModel.DataAnnotations;

namespace SchoolManager.ViewModels;

/// <summary>
/// Perfil de autogestión para personal institucional (credencial / carnet de personal).
/// </summary>
public class StaffInstitutionalProfileViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "El apellido es obligatorio")]
    [StringLength(100)]
    [Display(Name = "Apellido")]
    public string LastName { get; set; } = null!;

    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
    [StringLength(100)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = null!;

    [StringLength(50)]
    [Display(Name = "Cédula / documento")]
    public string? DocumentId { get; set; }

    [Display(Name = "Fecha de nacimiento")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Phone(ErrorMessage = "El formato del teléfono no es válido")]
    [StringLength(20)]
    [Display(Name = "Teléfono principal")]
    public string? CellphonePrimary { get; set; }

    [Phone(ErrorMessage = "El formato del teléfono no es válido")]
    [StringLength(20)]
    [Display(Name = "Teléfono secundario")]
    public string? CellphoneSecondary { get; set; }

    [Display(Name = "Tipo de sangre")]
    [StringLength(10)]
    public string? BloodType { get; set; }

    [Display(Name = "Alergias")]
    [StringLength(500)]
    public string? Allergies { get; set; }

    [Display(Name = "Contacto de emergencia")]
    [StringLength(200)]
    public string? EmergencyContactName { get; set; }

    [Display(Name = "Teléfono emergencia")]
    [StringLength(30)]
    public string? EmergencyContactPhone { get; set; }

    [Display(Name = "Relación")]
    [StringLength(50)]
    public string? EmergencyRelationship { get; set; }

    [StringLength(200)]
    [Display(Name = "Cargo")]
    public string? JobTitle { get; set; }

    [StringLength(200)]
    [Display(Name = "Departamento / área")]
    public string? Department { get; set; }

    [StringLength(80)]
    [Display(Name = "Código institucional")]
    public string? EmployeeCode { get; set; }

    [Display(Name = "Escuela")]
    public string? SchoolName { get; set; }

    [Display(Name = "Rol")]
    public string? RoleDisplay { get; set; }

    [Display(Name = "Estado")]
    public string? Status { get; set; }

    [Display(Name = "Foto")]
    public string? PhotoUrl { get; set; }

    public bool HasSchoolAssigned { get; set; }

    public bool CanOpenInstitutionalCredentialUi { get; set; }

    public string FullName => $"{Name} {LastName}".Trim();
}
