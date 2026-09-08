using System;
using System.Collections.Generic;

namespace SchoolManager.Models;

public partial class User
{
    public Guid Id { get; set; }

    public Guid? SchoolId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? Status { get; set; }

    public bool? TwoFactorEnabled { get; set; }

    public DateTime? LastLogin { get; set; }

    public DateTime CreatedAt { get; set; }

    public string LastName { get; set; } = null!;

    public string? DocumentId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string? CellphonePrimary { get; set; }

    public string? CellphoneSecondary { get; set; }

    public bool? Disciplina { get; set; }

    public string? Inclusion { get; set; }

    public bool? Orientacion { get; set; }

    public bool? Inclusivo { get; set; }

    /// <summary>Permite a una secretaria editar horas de carga curricular. No otorga rol admin.</summary>
    public bool? CanEditCurriculumLoad { get; set; }
    
    public string? Shift { get; set; } // Jornada actual del estudiante: Mañana, Tarde, Noche

    /// <summary>Tipo de sangre (A+, O-, etc.); opcional.</summary>
    public string? BloodType { get; set; }

    /// <summary>Alergias o información médica relevante del estudiante.</summary>
    public string? Allergies { get; set; }

    /// <summary>Nombre del contacto de emergencia.</summary>
    public string? EmergencyContactName { get; set; }

    /// <summary>Teléfono del contacto de emergencia.</summary>
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Relación del contacto de emergencia con el estudiante (ej. Padre, Madre, Acudiente).</summary>
    public string? EmergencyRelationship { get; set; }

    /// <summary>Estado del último envío masivo de contraseña por correo (Pending, Sent, Failed).</summary>
    public string? PasswordEmailStatus { get; set; }

    /// <summary>Fecha UTC del último intento de envío de contraseña por correo.</summary>
    public DateTime? PasswordEmailSentAt { get; set; }

    /// <summary>URL o path de la foto del usuario (solo asignable vía dominio).</summary>
    public string? PhotoUrl { get; private set; }

    /// <summary>Actualiza la foto del usuario (comportamiento de dominio).</summary>
    public void UpdatePhoto(string? photoUrl)
    {
        PhotoUrl = photoUrl;
    }

    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();

    public virtual ICollection<Attendance> AttendanceStudents { get; set; } = new List<Attendance>();

    public virtual ICollection<Attendance> AttendanceTeachers { get; set; } = new List<Attendance>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<DisciplineReport> DisciplineReportStudents { get; set; } = new List<DisciplineReport>();

    public virtual ICollection<DisciplineReport> DisciplineReportTeachers { get; set; } = new List<DisciplineReport>();

    public virtual ICollection<OrientationReport> OrientationReportStudents { get; set; } = new List<OrientationReport>();

    public virtual ICollection<OrientationReport> OrientationReportTeachers { get; set; } = new List<OrientationReport>();

    public virtual School? SchoolNavigation { get; set; }

    public virtual ICollection<StudentActivityScore> StudentActivityScores { get; set; } = new List<StudentActivityScore>();

    public virtual ICollection<StudentAssignment> StudentAssignments { get; set; } = new List<StudentAssignment>();

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();

    public virtual ICollection<TeacherAssignment> TeacherAssignments { get; set; } = new List<TeacherAssignment>();

    public virtual ICollection<GradeLevel> Grades { get; set; } = new List<GradeLevel>();

    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    public virtual ICollection<Subject> Subjects { get; set; } = new List<Subject>();

    public virtual User? CreatedByUser { get; set; }

    public virtual User? UpdatedByUser { get; set; }
}
