using System;
using System.Collections.Generic;

namespace SchoolManager.Models;

public partial class StudentAssignment
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid GradeId { get; set; }

    public Guid GroupId { get; set; }

    public Guid? ShiftId { get; set; } // Referencia a la tabla de jornadas

    public DateTime? CreatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime? EndDate { get; set; }

    public Guid? AcademicYearId { get; set; }

    /// <summary>Tipo de matrícula: Regular, Nocturno, Refuerzo, Libre.</summary>
    public string EnrollmentType { get; set; } = "Regular";

    /// <summary>Inicio efectivo de esta matrícula (por defecto alineado con <see cref="CreatedAt"/>).</summary>
    public DateTime? StartDate { get; set; }

    public virtual GradeLevel Grade { get; set; } = null!;

    public virtual Group Group { get; set; } = null!;

    public virtual Shift? Shift { get; set; } // Navegación a la jornada

    public virtual User Student { get; set; } = null!;

    public virtual AcademicYear? AcademicYear { get; set; }
}
