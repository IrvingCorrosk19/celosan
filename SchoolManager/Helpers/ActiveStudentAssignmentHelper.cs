using SchoolManager.Models;

namespace SchoolManager.Helpers;

/// <summary>
/// Cuando un estudiante tiene varias <see cref="StudentAssignment"/> activas (p. ej. regular + nocturna),
/// elige una fila coherente para carnet, escaneo y listados. Las consultas LINQ correlacionadas en EF
/// repiten el mismo criterio de orden inline (no se puede reutilizar un método arbitrario en el árbol de expresión).
/// </summary>
public static class ActiveStudentAssignmentHelper
{
    /// <summary>
    /// Prioridad: matrícula tipo Nocturno, jornada cuyo nombre contiene "Noche", luego la asignación más reciente.
    /// </summary>
    public static StudentAssignment? PickForDisplay(IEnumerable<StudentAssignment> assignments)
    {
        var list = assignments.Where(a => a.IsActive).ToList();
        if (list.Count == 0)
            return null;

        return list
            .OrderByDescending(MatchesNocturnoEnrollment)
            .ThenByDescending(MatchesNightShiftName)
            .ThenByDescending(a => a.CreatedAt ?? DateTime.MinValue)
            .First();
    }

    private static bool MatchesNocturnoEnrollment(StudentAssignment a) =>
        !string.IsNullOrWhiteSpace(a.EnrollmentType) &&
        a.EnrollmentType.Trim().Equals("Nocturno", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesNightShiftName(StudentAssignment a) =>
        !string.IsNullOrWhiteSpace(a.Shift?.Name) &&
        a.Shift!.Name.Contains("Noche", StringComparison.OrdinalIgnoreCase);
}
