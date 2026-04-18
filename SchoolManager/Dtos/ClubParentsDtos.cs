using System;

namespace SchoolManager.Dtos;

/// <summary>Estudiante con estado de carnet y plataforma para listado Club de Padres.</summary>
public class ClubParentsStudentDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    /// <summary>Cédula u otro documento (columna document_id del usuario).</summary>
    public string? DocumentId { get; set; }
    public string Grade { get; set; } = "";
    public string Group { get; set; } = "";
    public string CarnetStatus { get; set; } = "Pendiente";
    public string PlatformAccessStatus { get; set; } = "Pendiente";
}

/// <summary>Estado de pago/acceso de un estudiante.</summary>
public class StudentPaymentStatusDto
{
    public Guid StudentId { get; set; }
    public string CarnetStatus { get; set; } = "Pendiente";
    public string PlatformAccessStatus { get; set; } = "Pendiente";
    public DateTime? CarnetStatusUpdatedAt { get; set; }
    public DateTime? PlatformStatusUpdatedAt { get; set; }
}

/// <summary>Carnet pagado pendiente de impresión (QL Services).</summary>
public class PendingPrintItemDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = "";
    public string Grade { get; set; } = "";
    public string Group { get; set; } = "";
    public string CarnetStatus { get; set; } = "Pagado";
    public DateTime? CarnetStatusUpdatedAt { get; set; }
}

/// <summary>Request con StudentId para POST MarkPaid, Activate, MarkPrinted, MarkDelivered.</summary>
public class StudentIdRequest
{
    public Guid StudentId { get; set; }
}
