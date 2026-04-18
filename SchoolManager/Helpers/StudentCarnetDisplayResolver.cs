using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;

namespace SchoolManager.Helpers;

/// <summary>
/// Si no hay <see cref="StudentAssignment"/> activa, el carnet puede mostrar grado/grupo/jornada
/// a partir de <see cref="StudentSubjectAssignment"/> + <see cref="SubjectAssignment"/> (carga masiva por materias).
/// </summary>
public static class StudentCarnetDisplayResolver
{
    public sealed class Context
    {
        public string GradeName { get; init; } = "";
        public string GroupName { get; init; } = "";
        public string ShiftDisplay { get; init; } = "N/A";
        public string? AcademicYearName { get; init; }
        public string? AdditionalEnrollmentsSummary { get; init; }
        /// <summary>Ids de <see cref="SubjectAssignment"/> (mismo significado que en matrícula grado/grupo).</summary>
        public Guid GradeLevelId { get; init; }
        public Guid GroupId { get; init; }
    }

    /// <summary>
    /// Elige la mejor fila activa de inscripción por materia (prioridad alineada con nocturna).
    /// </summary>
    public static async Task<Context?> TryResolveFromSubjectEnrollmentsAsync(
        SchoolDbContext context,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var list = await context.StudentSubjectAssignments
            .AsNoTracking()
            .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
            .Include(ssa => ssa.SubjectAssignment)
                .ThenInclude(sa => sa!.GradeLevel)
            .Include(ssa => ssa.SubjectAssignment)
                .ThenInclude(sa => sa!.Group)
                    .ThenInclude(g => g!.ShiftNavigation)
            .Include(ssa => ssa.Shift)
            .Include(ssa => ssa.AcademicYear)
            .ToListAsync(cancellationToken);

        if (list.Count == 0)
            return null;

        var best = list
            .OrderByDescending(MatchesNocturnoEnrollment)
            .ThenByDescending(MatchesNightShiftOnSsa)
            .ThenByDescending(MatchesNightShiftOnGroup)
            .ThenByDescending(ssa => ssa.CreatedAt ?? DateTime.MinValue)
            .First();

        var sa = best.SubjectAssignment;
        if (sa?.GradeLevel == null || sa.Group == null)
            return null;

        var gradeName = sa.GradeLevel.Name ?? "—";
        var groupName = sa.Group.Name ?? "—";
        var shiftDisplay = best.Shift?.Name?.Trim()
            ?? sa.Group.ShiftNavigation?.Name?.Trim()
            ?? sa.Group.Shift?.Trim()
            ?? "N/A";

        var academicYearName = best.AcademicYear?.Name;

        var additional = BuildAdditionalSummary(list, best);

        return new Context
        {
            GradeName = gradeName,
            GroupName = groupName,
            ShiftDisplay = string.IsNullOrWhiteSpace(shiftDisplay) ? "N/A" : shiftDisplay,
            AcademicYearName = academicYearName,
            AdditionalEnrollmentsSummary = additional,
            GradeLevelId = sa.GradeLevelId,
            GroupId = sa.GroupId
        };
    }

    private static bool MatchesNocturnoEnrollment(StudentSubjectAssignment ssa) =>
        !string.IsNullOrWhiteSpace(ssa.EnrollmentType) &&
        ssa.EnrollmentType.Trim().Equals("Nocturno", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesNightShiftOnSsa(StudentSubjectAssignment ssa) =>
        !string.IsNullOrWhiteSpace(ssa.Shift?.Name) &&
        ssa.Shift!.Name.Contains("Noche", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesNightShiftOnGroup(StudentSubjectAssignment ssa)
    {
        var g = ssa.SubjectAssignment?.Group;
        if (g?.ShiftNavigation?.Name != null &&
            g.ShiftNavigation.Name.Contains("Noche", StringComparison.OrdinalIgnoreCase))
            return true;
        return !string.IsNullOrWhiteSpace(g?.Shift) &&
               g.Shift.Contains("Noche", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildAdditionalSummary(
        List<StudentSubjectAssignment> all,
        StudentSubjectAssignment primary)
    {
        var others = all
            .Where(o => o.Id != primary.Id)
            .ToList();
        if (others.Count == 0)
            return null;

        var parts = others
            .Select(o =>
            {
                var sa = o.SubjectAssignment;
                var g = sa?.GradeLevel?.Name ?? "—";
                var gr = sa?.Group?.Name ?? "—";
                var sh = o.Shift?.Name?.Trim()
                    ?? sa?.Group?.ShiftNavigation?.Name?.Trim()
                    ?? sa?.Group?.Shift?.Trim();
                return string.IsNullOrWhiteSpace(sh) ? $"{g} — {gr}" : $"{g} — {gr} ({sh})";
            })
            .Distinct()
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
