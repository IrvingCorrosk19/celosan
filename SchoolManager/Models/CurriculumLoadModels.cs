using System;
using System.Collections.Generic;

namespace SchoolManager.Models;

public partial class CurriculumBlock
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CurriculumLoadHours> Hours { get; set; } = new List<CurriculumLoadHours>();
}

public partial class CurriculumLoadSubject
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SpecialtyId { get; set; }
    public Guid GradeLevelId { get; set; }
    public Guid AreaId { get; set; }
    public Guid SubjectId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Specialty Specialty { get; set; } = null!;
    public virtual GradeLevel GradeLevel { get; set; } = null!;
    public virtual Area Area { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual User? CreatedByUser { get; set; }
    public virtual User? UpdatedByUser { get; set; }
    public virtual ICollection<CurriculumLoadHours> Hours { get; set; } = new List<CurriculumLoadHours>();
}

public partial class CurriculumLoadHours
{
    public Guid Id { get; set; }
    public Guid CurriculumLoadSubjectId { get; set; }
    public Guid CurriculumBlockId { get; set; }
    public decimal Hours { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual CurriculumLoadSubject CurriculumLoadSubject { get; set; } = null!;
    public virtual CurriculumBlock CurriculumBlock { get; set; } = null!;
    public virtual User? CreatedByUser { get; set; }
    public virtual User? UpdatedByUser { get; set; }
}
