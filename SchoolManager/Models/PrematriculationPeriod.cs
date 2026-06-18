using System;

namespace SchoolManager.Models;

public partial class PrematriculationPeriod
{
    public Guid Id { get; set; }
    
    public Guid SchoolId { get; set; }

    public string? Name { get; set; }

    public Guid? AcademicYearId { get; set; }

    public Guid? TrimesterId { get; set; }
    
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
    
    public bool IsActive { get; set; }
    
    public int MaxCapacityPerGroup { get; set; }

    public int? MaxSubjectsAllowed { get; set; }
    
    public bool AutoAssignByShift { get; set; }
    
    public decimal RequiredAmount { get; set; } // Monto total requerido para completar el pago
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public Guid? CreatedBy { get; set; }
    
    public Guid? UpdatedBy { get; set; }
    
    public virtual School School { get; set; } = null!;

    public virtual AcademicYear? AcademicYear { get; set; }

    public virtual Trimester? Trimester { get; set; }
    
    public virtual User? CreatedByUser { get; set; }
    
    public virtual User? UpdatedByUser { get; set; }
    
    public virtual ICollection<Prematriculation> Prematriculations { get; set; } = new List<Prematriculation>();

    public virtual ICollection<StudentPrematriculationSubjectSelection> SubjectSelections { get; set; } = new List<StudentPrematriculationSubjectSelection>();

    public virtual ICollection<PrematriculationReceipt> Receipts { get; set; } = new List<PrematriculationReceipt>();

    public virtual ICollection<PrematriculationReopenAuthorization> ReopenAuthorizations { get; set; } = new List<PrematriculationReopenAuthorization>();
}

