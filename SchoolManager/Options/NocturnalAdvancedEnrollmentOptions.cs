namespace SchoolManager.Options;

/// <summary>
/// Feature flag por escuela para matrícula nocturna avanzada (arrastre, multi-grupo, multi-nivel).
/// Colegios regulares: mantener desactivado (default).
/// </summary>
public class NocturnalAdvancedEnrollmentOptions
{
    public const string SectionName = "NocturnalAdvancedEnrollment";

    /// <summary>Si true, todas las escuelas usan el modo avanzado (solo para entornos nocturnos dedicados).</summary>
    public bool EnableForAllSchools { get; set; }

    /// <summary>IDs de escuelas con modo avanzado habilitado.</summary>
    public List<string> EnabledSchoolIds { get; set; } = new();
}
