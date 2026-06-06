namespace SchoolManager.Helpers;

/// <summary>Valores de <see cref="Models.StudentAssignment.EnrollmentType"/> y SSA.</summary>
public static class EnrollmentTypeConstants
{
    public const string Regular = "Regular";
    public const string Nocturno = "Nocturno";
    public const string Refuerzo = "Refuerzo";
    public const string Libre = "Libre";

    /// <summary>Tipo por defecto de matrícula e inscripción en Eduplaner Noche.</summary>
    public const string DefaultPrimary = Nocturno;

    public static bool IsCarryOver(string? enrollmentType) =>
        !string.IsNullOrWhiteSpace(enrollmentType) &&
        (enrollmentType.Trim().Equals(Refuerzo, StringComparison.OrdinalIgnoreCase) ||
         enrollmentType.Trim().Equals(Libre, StringComparison.OrdinalIgnoreCase));

    public static bool IsPrimaryLevel(string? enrollmentType) =>
        string.IsNullOrWhiteSpace(enrollmentType) ||
        enrollmentType.Trim().Equals(Regular, StringComparison.OrdinalIgnoreCase) ||
        enrollmentType.Trim().Equals(Nocturno, StringComparison.OrdinalIgnoreCase);

    public static string NormalizePrimary(string? enrollmentType) =>
        string.IsNullOrWhiteSpace(enrollmentType) ? DefaultPrimary : enrollmentType.Trim();

    public static string ResolveSubjectEnrollmentType(string? explicitType, bool isCarryOverGrade) =>
        !string.IsNullOrWhiteSpace(explicitType)
            ? explicitType.Trim()
            : isCarryOverGrade ? Refuerzo : DefaultPrimary;

    public static int? ParseGradeNumber(string? gradeName)
    {
        if (string.IsNullOrWhiteSpace(gradeName))
            return null;

        var digits = new string(gradeName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : null;
    }
}
