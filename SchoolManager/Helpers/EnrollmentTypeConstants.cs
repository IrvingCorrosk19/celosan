namespace SchoolManager.Helpers;

/// <summary>Valores de <see cref="Models.StudentAssignment.EnrollmentType"/> y SSA.</summary>
public static class EnrollmentTypeConstants
{
    public const string Regular = "Regular";
    public const string Nocturno = "Nocturno";
    public const string Refuerzo = "Refuerzo";
    public const string Libre = "Libre";

    public static bool IsCarryOver(string? enrollmentType) =>
        !string.IsNullOrWhiteSpace(enrollmentType) &&
        (enrollmentType.Trim().Equals(Refuerzo, StringComparison.OrdinalIgnoreCase) ||
         enrollmentType.Trim().Equals(Libre, StringComparison.OrdinalIgnoreCase));

    public static bool IsPrimaryLevel(string? enrollmentType) =>
        string.IsNullOrWhiteSpace(enrollmentType) ||
        enrollmentType.Trim().Equals(Regular, StringComparison.OrdinalIgnoreCase) ||
        enrollmentType.Trim().Equals(Nocturno, StringComparison.OrdinalIgnoreCase);
}
