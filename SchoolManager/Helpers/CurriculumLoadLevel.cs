namespace SchoolManager.Helpers;

public static class CurriculumLoadLevel
{
    public const string Premedia = "PREMEDIA";
    public const string Media = "MEDIA";
    public const string ViewFullProgram = "FullProgram";
    public const string ViewByGrade = "ByGrade";
    public const string BlockB1 = "B1";
    public const string BlockB2 = "B2";

    public static bool IsPremediaGrade(int gradeNumber) => gradeNumber is 7 or 8 or 9;

    public static bool IsMediaGrade(int gradeNumber) => gradeNumber is 10 or 11 or 12;

    public static string? ResolveProgramType(IEnumerable<int> gradeNumbers)
    {
        var nums = gradeNumbers.Where(n => n > 0).ToHashSet();
        if (nums.Any(IsPremediaGrade))
            return Premedia;
        if (nums.Any(IsMediaGrade))
            return Media;
        return null;
    }

    public static IReadOnlyList<int> ExpectedGrades(string? programType) =>
        string.Equals(programType, Premedia, StringComparison.OrdinalIgnoreCase)
            ? new[] { 7, 8, 9 }
            : new[] { 10, 11, 12 };

    public static string FormatGradeLabel(int gradeNumber) => $"{gradeNumber}°";

    public static string HeaderTitle(string programType, string programName) =>
        string.Equals(programType, Premedia, StringComparison.OrdinalIgnoreCase)
            ? "PRE-MEDIA"
            : programName;
}
