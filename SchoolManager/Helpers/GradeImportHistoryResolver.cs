namespace SchoolManager.Helpers;

/// <summary>
/// Resuelve matrícula y SSA históricas del importador sin elegir al azar
/// y sin cambiar el grado/grupo del Excel.
/// </summary>
public static class GradeImportHistoryResolver
{
    public static (T? Selected, string? ErrorCode) SelectEnrollment<T>(
        IReadOnlyList<T> candidates,
        Func<T, bool> isActive,
        Func<T, DateTime?> endDate)
    {
        if (candidates == null || candidates.Count == 0)
            return (default, GradeImportErrorCodes.EnrollmentNotFound);

        var active = candidates.Where(isActive).ToList();
        if (active.Count == 1)
            return (active[0], null);
        if (active.Count > 1)
            return (default, GradeImportErrorCodes.EnrollmentAmbiguous);

        var historical = candidates.Where(x => !isActive(x)).ToList();
        if (historical.Count == 0)
            return (default, GradeImportErrorCodes.EnrollmentNotFound);
        if (historical.Count == 1)
            return (historical[0], null);

        var dated = historical.Where(x => endDate(x).HasValue).ToList();
        if (dated.Count == 0)
            return (default, GradeImportErrorCodes.EnrollmentAmbiguous);

        var latest = dated.Max(x => endDate(x)!.Value);
        var top = dated.Where(x => endDate(x) == latest).ToList();
        return top.Count == 1
            ? (top[0], null)
            : (default, GradeImportErrorCodes.EnrollmentAmbiguous);
    }

    public static (T? Selected, string? ErrorCode) SelectSubject<T>(
        IReadOnlyList<T> namedHits,
        bool enrollmentIsActive,
        Func<T, bool> isActive)
    {
        if (namedHits == null || namedHits.Count == 0)
            return (default, GradeImportErrorCodes.SsaNotFound);

        var active = namedHits.Where(isActive).ToList();
        if (enrollmentIsActive)
        {
            if (active.Count == 1)
                return (active[0], null);
            if (active.Count > 1)
                return (default, GradeImportErrorCodes.SubjectAmbiguous);
            return (default, GradeImportErrorCodes.SsaInactive);
        }

        if (active.Count == 1)
            return (active[0], null);
        if (active.Count > 1)
            return (default, GradeImportErrorCodes.SubjectAmbiguous);
        if (namedHits.Count == 1)
            return (namedHits[0], null);
        return (default, GradeImportErrorCodes.SubjectAmbiguous);
    }
}
