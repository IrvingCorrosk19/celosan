using SchoolManager.ViewModels;

namespace SchoolManager.Helpers;

/// <summary>Utilidades para carga masiva nocturna (multi-nivel / multi-grupo / materias selectivas).</summary>
public static class BulkNightEnrollmentHelper
{
    private static readonly Dictionary<string, string> SubjectAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EXPRESIONES ARTISTICAS"] = "EXPRESIONES ARTÍSTICA",
        ["EXPRESIONES ARTÍSTICAS"] = "EXPRESIONES ARTÍSTICA",
        ["EXPRESIONES ARTISTICA"] = "EXPRESIONES ARTÍSTICA",
        ["VAL. ETICOS / REL. HUMANAS"] = "ÉTICA MORAL, VALORES Y RELACIONES HUMANAS",
        ["VAL. ÉTICOS / REL. HUMANAS"] = "ÉTICA MORAL, VALORES Y RELACIONES HUMANAS",
        ["ETICA MORAL, VALORES Y RELACIONES HUMANAS"] = "ÉTICA MORAL, VALORES Y RELACIONES HUMANAS",
    };

    public static string NormalizeCatalogToken(string? input)
    {
        input ??= string.Empty;
        input = input.Trim();
        input = input.Normalize(System.Text.NormalizationForm.FormD);
        input = new string(input.Where(ch =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) !=
            System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return input;
    }

    public static string NormalizeGradeLabel(string? nivel)
    {
        if (string.IsNullOrWhiteSpace(nivel))
            return string.Empty;

        var text = nivel.Trim();
        if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numeric))
            return ((int)numeric).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return EnrollmentTypeConstants.ParseGradeNumber(text)?.ToString(System.Globalization.CultureInfo.InvariantCulture)
               ?? text.TrimEnd('°', 'º', '.', ' ').Trim();
    }

    public static string ResolveSubjectLookupName(string rawName)
    {
        var trimmed = rawName.Trim();
        if (SubjectAliases.TryGetValue(trimmed, out var alias))
            return alias;
        return trimmed;
    }

    public static string NormalizeSubjectKey(string? name) =>
        NormalizeCatalogToken(name).ToUpperInvariant();

    /// <summary>Una fila por estudiante + materia + nivel + grupo; prioriza Inscrito = true.</summary>
    public static List<StudentSubjectEnrollmentInputModel> DedupeRows(IEnumerable<StudentSubjectEnrollmentInputModel> rows) =>
        rows
            .GroupBy(r => (
                Email: (r.EstudianteEmail ?? string.Empty).Trim().ToLowerInvariant(),
                Subject: NormalizeSubjectKey(r.Asignatura),
                Nivel: NormalizeGradeLabel(r.Nivel).ToUpperInvariant(),
                Grupo: (r.GrupoAcademico ?? string.Empty).Trim().ToUpperInvariant()))
            .Select(g => g.OrderByDescending(r => r.Inscrito).First())
            .ToList();

    public static string NormalizeCatalogField(string? input) =>
        NormalizeCatalogToken(input).ToUpperInvariant();

    public static string BuildCatalogCombinationKey(
        string especialidad, string area, string materia, string grado, string grupo) =>
        $"{NormalizeCatalogField(especialidad)}|{NormalizeCatalogField(area)}|{NormalizeSubjectKey(materia)}|{NormalizeGradeLabel(grado).ToUpperInvariant()}|{NormalizeCatalogField(grupo)}";

    /// <summary>Una fila por especialidad + área + materia + grado + grupo.</summary>
    public static List<AcademicCatalogInputModel> DedupeCatalogRows(IEnumerable<AcademicCatalogInputModel> rows) =>
        rows
            .GroupBy(r => BuildCatalogCombinationKey(r.Especialidad, r.Area, r.Materia, r.Grado, r.Grupo))
            .Select(g => g.First())
            .ToList();
}
