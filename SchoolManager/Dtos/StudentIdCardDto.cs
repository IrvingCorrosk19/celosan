namespace SchoolManager.Dtos;

public class StudentIdCardDto
{
    public Guid StudentId { get; set; }
    public string CardNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Grade { get; set; } = null!;
    public string Group { get; set; } = null!;
    public string Shift { get; set; } = null!;

    /// <summary>Otros contextos de matrícula activa (misma prioridad nocturna que el primario).</summary>
    public string? AdditionalEnrollmentsSummary { get; set; }

    public string QrToken { get; set; } = null!;
    /// <summary>QR generado en el servidor como data URI (data:image/png;base64,...). Evita dependencia de CDN en la vista.</summary>
    public string? QrImageDataUrl { get; set; }

    /// <summary>QR con URL https firmada: al escanear abre datos de emergencia e información personal (cualquier lector).</summary>
    public string? EmergencyInfoQrImageDataUrl { get; set; }
    /// <summary>URL o path de la foto del estudiante (para vista y PDF).</summary>
    public string? PhotoUrl { get; set; }
}
