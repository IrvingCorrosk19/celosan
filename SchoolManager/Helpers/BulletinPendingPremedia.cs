namespace SchoolManager.Helpers;

/// <summary>
/// Detecta materias de Premedia realmente pendientes para un alumno que ya está en Media.
/// No inventa filas: solo clasifica inscripciones existentes.
/// </summary>
public static class BulletinPendingPremedia
{
    public static bool IsPending(
        int primaryGradeNumber,
        int subjectGradeNumber,
        bool ssaIsActive,
        string? ssaEnrollmentType,
        bool subjectGradeHasPrimaryRegularEnrollment)
    {
        if (!ssaIsActive)
            return false;
        if (!CurriculumLoadLevel.IsMediaGrade(primaryGradeNumber))
            return false;
        if (!CurriculumLoadLevel.IsPremediaGrade(subjectGradeNumber))
            return false;
        if (EnrollmentTypeConstants.IsCarryOver(ssaEnrollmentType))
            return true;
        return !subjectGradeHasPrimaryRegularEnrollment;
    }
}
