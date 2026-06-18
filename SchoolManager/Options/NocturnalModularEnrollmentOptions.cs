namespace SchoolManager.Options;

/// <summary>
/// Feature flag del modelo academico nocturno modular por trimestre.
/// Si esta apagado, todos los flujos existentes conservan el comportamiento legacy.
/// </summary>
public class NocturnalModularEnrollmentOptions
{
    public const string SectionName = "NocturnalModularEnrollment";

    public bool Enabled { get; set; } = false;

    public List<string> EnabledSchoolIds { get; set; } = new();
}
