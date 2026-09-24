using System;
using System.Collections.Generic;
using SchoolManager.Helpers;

namespace SchoolManager.Dtos;

public static class GradeImportStatus
{
    public const string Nuevo = "NUEVO";
    public const string Actualizar = "ACTUALIZAR";
    public const string SinCambios = "SIN CAMBIOS";
    public const string Error = "ERROR";
}

public static class GradeImportCurrentOrigin
{
    public const string Activities = "Actividades";
    public const string Imported = "Importada";
    public const string None = "Sin nota";
}

public class GradeImportPageDto
{
    public Guid? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? SchoolError { get; set; }
    public List<GradeImportYearOptionDto> Years { get; set; } = new();
}

public class GradeImportYearOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GradeImportPreviewDto
{
    public string PreviewToken { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalOperations { get; set; }
    public int NewCount { get; set; }
    public int UpdateCount { get; set; }
    public int UnchangedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<GradeImportOperationDto> Operations { get; set; } = new();
}

public class GradeImportOperationDto
{
    public string Status { get; set; } = GradeImportStatus.Error;
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ExcelValue { get; set; }
    public string? FoundValue { get; set; }
    public string? ReviewHint { get; set; }
    public int RowNumber { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public string Trimester { get; set; } = string.Empty;
    public string CurrentOrigin { get; set; } = GradeImportCurrentOrigin.None;
    public decimal? CurrentOfficialScore { get; set; }
    public decimal? CurrentImportedScore { get; set; }
    public string CurrentStatus { get; set; } = ImportedTrimesterGradeStatus.Graded;
    public string CurrentDisplay { get; set; } = "—";
    public decimal? NewScore { get; set; }
    public string NewStatus { get; set; } = ImportedTrimesterGradeStatus.Graded;
    public string NewDisplay { get; set; } = "—";
}

public class GradeImportAnalyzeResult
{
    public bool FileError { get; set; }
    public string? FileErrorMessage { get; set; }
    public GradeImportPreviewDto? Preview { get; set; }
}

public class GradeImportPreviewCacheEntry
{
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid AcademicYearId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public GradeImportPreviewDto Preview { get; set; } = new();
    public List<GradeImportResolvedOperation> ResolvedOperations { get; set; } = new();
}

public class GradeImportResolvedOperation
{
    public GradeImportOperationDto Display { get; set; } = new();
    public Guid? StudentId { get; set; }
    public Guid? StudentAssignmentId { get; set; }
    public Guid? StudentSubjectAssignmentId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? TrimesterId { get; set; }
    public decimal? NewScore { get; set; }
    public string NewStatus { get; set; } = ImportedTrimesterGradeStatus.Graded;
}

public class GradeImportConfirmResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool TokenInvalid { get; set; }
    public bool HasPreviewErrors { get; set; }
    public bool RevalidationFailed { get; set; }
    public bool ConcurrentChange { get; set; }
    public GradeImportConfirmSummaryDto? Summary { get; set; }
}

public class GradeImportConfirmSummaryDto
{
    public Guid ImportBatchId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string AcademicYearName { get; set; } = string.Empty;
    public int Processed { get; set; }
    public int NewCount { get; set; }
    public int UpdateCount { get; set; }
    public int UnchangedCount { get; set; }
    public int ErrorCount { get; set; }
    public int OmittedCount { get; set; }
    public List<GradeImportOperationDto> OmittedOperations { get; set; } = new();
    public DateTime CompletedAt { get; set; }
    public string UserName { get; set; } = string.Empty;
}

public static class GradeImportConfirmMessages
{
    public const string TokenInvalid = "El análisis expiró o ya no es válido. Vuelva a analizar el archivo.";
    public const string PreviewHasErrors = "No hay filas válidas para importar. Corrija el archivo y vuelva a analizarlo.";
    public const string AcademicChanged = "La información académica cambió durante la importación. Vuelva a analizar el archivo.";
}
