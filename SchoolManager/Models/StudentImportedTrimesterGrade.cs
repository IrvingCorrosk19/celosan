using System;

namespace SchoolManager.Models;

/// <summary>
/// Nota trimestral consolidada importada (override académico).
/// No es una actividad del docente. Si existe, es la nota oficial del trimestre.
/// </summary>
public class StudentImportedTrimesterGrade
{
    public Guid Id { get; set; }

    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid StudentSubjectAssignmentId { get; set; }

    public Guid StudentAssignmentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid TrimesterId { get; set; }

    public decimal? Score { get; set; }

    public string Status { get; set; } = Helpers.ImportedTrimesterGradeStatus.Graded;

    public string Source { get; set; } = StudentImportedTrimesterGradeSource.ExcelImport;

    public Guid? ImportBatchId { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual School School { get; set; } = null!;

    public virtual User Student { get; set; } = null!;

    public virtual StudentSubjectAssignment StudentSubjectAssignment { get; set; } = null!;

    public virtual StudentAssignment StudentAssignment { get; set; } = null!;

    public virtual AcademicYear AcademicYear { get; set; } = null!;

    public virtual Trimester Trimester { get; set; } = null!;

    public virtual CelosanBulkImportLog? ImportBatch { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual User? UpdatedByUser { get; set; }
}

public static class StudentImportedTrimesterGradeSource
{
    public const string ExcelImport = "ExcelImport";
}
