namespace SchoolManager.Options;

/// <summary>
/// Configuración de matrícula nocturna avanzada (arrastre, multi-grupo, multi-nivel).
/// Eduplaner Noche opera con <see cref="EnableForAllSchools"/> activo por defecto.
/// </summary>
public class NocturnalAdvancedEnrollmentOptions
{
    public const string SectionName = "NocturnalAdvancedEnrollment";

    /// <summary>Si true, todas las escuelas usan el modo nocturno avanzado (default en Eduplaner Noche).</summary>
    public bool EnableForAllSchools { get; set; } = true;

    /// <summary>IDs de escuelas con modo avanzado habilitado.</summary>
    public List<string> EnabledSchoolIds { get; set; } = new();
}
