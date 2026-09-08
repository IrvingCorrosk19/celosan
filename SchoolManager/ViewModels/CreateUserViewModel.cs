using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SchoolManager.ViewModels
{
    public class CreateUserViewModel
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Name { get; set; } = null!;

        public string? LastName { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe ser un correo válido.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Identificación es obligatoria.")]
        public string? DocumentId { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? PasswordHash { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio.")]
        public string Role { get; set; } = null!;

        [Required(ErrorMessage = "El estado es obligatorio.")]
        public string Status { get; set; } = null!;

        [Phone(ErrorMessage = "El formato del celular principal no es válido.")]
        [Display(Name = "Celular Principal")]
        public string? CellphonePrimary { get; set; }

        [Phone(ErrorMessage = "El formato del celular secundario no es válido.")]
        [Display(Name = "Celular Secundario")]
        public string? CellphoneSecondary { get; set; }

        [Display(Name = "Disciplina")]
        public bool? Disciplina { get; set; }

        [Display(Name = "Inclusión")]
        public string? Inclusion { get; set; }

        [Display(Name = "Orientación")]
        public bool? Orientacion { get; set; }

        [Display(Name = "Inclusivo")]
        public bool? Inclusivo { get; set; }

        [Display(Name = "Editar carga horaria")]
        public bool? CanEditCurriculumLoad { get; set; }

        public List<Guid> Subjects { get; set; } = new();
        public List<Guid> Groups { get; set; } = new();
    }
}
