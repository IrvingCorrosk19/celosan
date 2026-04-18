using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class ScheduleService : IScheduleService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ScheduleService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ScheduleEntry> CreateEntryAsync(
        Guid teacherAssignmentId,
        Guid timeSlotId,
        byte dayOfWeek,
        Guid academicYearId,
        Guid currentUserId)
    {
        if (dayOfWeek < 1 || dayOfWeek > 7)
            throw new ArgumentException("DayOfWeek debe estar entre 1 (Lunes) y 7 (Domingo).", nameof(dayOfWeek));

        var ta = await _context.TeacherAssignments
            .Include(t => t.SubjectAssignment)
            .Include(t => t.Teacher)
            .FirstOrDefaultAsync(t => t.Id == teacherAssignmentId)
            .ConfigureAwait(false);

        if (ta == null)
            throw new InvalidOperationException("No se encontró la asignación docente indicada.");

        var timeSlot = await _context.TimeSlots.FindAsync(timeSlotId).ConfigureAwait(false);
        if (timeSlot == null)
            throw new InvalidOperationException("No se encontró el bloque horario indicado.");

        var academicYear = await _context.AcademicYears.FindAsync(academicYearId).ConfigureAwait(false);
        if (academicYear == null)
            throw new InvalidOperationException("No se encontró el año académico indicado.");

        // C) Seguridad: Teacher solo puede crear horarios de sus propias TeacherAssignments
        var role = await _currentUserService.GetCurrentUserRoleAsync().ConfigureAwait(false);
        var isTeacher = string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(role, "docente", StringComparison.OrdinalIgnoreCase);
        if (isTeacher && ta.TeacherId != currentUserId)
            throw new UnauthorizedAccessException("Solo puede asignar horarios a sus propias materias. La asignación docente no le pertenece.");

        var slotLabel = FormatTimeSlotLabel(timeSlot);
        var dayLabel = SpanishDayName(dayOfWeek);

        // A) Conflicto docente: mismo docente no puede tener mismo año + día + bloque
        var blockingForTeacher = await _context.ScheduleEntries
            .AsNoTracking()
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(sa => sa!.Subject)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(sa => sa!.Group)
            .FirstOrDefaultAsync(e =>
                e.AcademicYearId == academicYearId &&
                e.DayOfWeek == dayOfWeek &&
                e.TimeSlotId == timeSlotId &&
                e.TeacherAssignment.TeacherId == ta.TeacherId,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (blockingForTeacher != null)
        {
            var o = blockingForTeacher.TeacherAssignment!;
            var sa0 = o.SubjectAssignment!;
            var subject = sa0.Subject?.Name ?? "otra materia";
            var group = sa0.Group?.Name ?? "—";
            throw new InvalidOperationException(
                $"Conflicto de horario: este docente ya tiene asignada {subject} (grupo {group}) el {dayLabel} en {slotLabel} para este año académico.");
        }

        // B) Conflicto grupo: mismo grupo no puede tener mismo año + día + bloque (vía otra TeacherAssignment -> mismo GroupId)
        var groupId = ta.SubjectAssignment.GroupId;
        var blockingForGroup = await _context.ScheduleEntries
            .AsNoTracking()
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.Teacher)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(sa => sa!.Subject)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(sa => sa!.Group)
            .FirstOrDefaultAsync(e =>
                e.AcademicYearId == academicYearId &&
                e.DayOfWeek == dayOfWeek &&
                e.TimeSlotId == timeSlotId &&
                e.TeacherAssignment.SubjectAssignment.GroupId == groupId,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (blockingForGroup != null)
        {
            var o = blockingForGroup.TeacherAssignment!;
            var sa0 = o.SubjectAssignment!;
            var subject = sa0.Subject?.Name ?? "una clase";
            var groupName = sa0.Group?.Name ?? "el grupo";
            var teacher = o.Teacher;
            var teacherName = teacher != null
                ? $"{teacher.Name} {teacher.LastName}".Trim()
                : "otro docente";
            throw new InvalidOperationException(
                $"Conflicto de horario: el grupo {groupName} ya tiene ocupado este espacio el {dayLabel} en {slotLabel} con {subject} (docente: {teacherName}) para este año académico.");
        }

        var entry = new ScheduleEntry
        {
            Id = Guid.NewGuid(),
            TeacherAssignmentId = teacherAssignmentId,
            TimeSlotId = timeSlotId,
            DayOfWeek = dayOfWeek,
            AcademicYearId = academicYearId,
            CreatedBy = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ScheduleEntries.Add(entry);
        await _context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        return await _context.ScheduleEntries
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(s => s!.Subject)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.Teacher)
            .Include(e => e.TimeSlot)
            .Include(e => e.AcademicYear)
            .FirstAsync(e => e.Id == entry.Id, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task DeleteEntryAsync(Guid id, Guid currentUserId)
    {
        var entry = await _context.ScheduleEntries
            .Include(e => e.TeacherAssignment)
            .FirstOrDefaultAsync(e => e.Id == id, CancellationToken.None)
            .ConfigureAwait(false);

        if (entry == null)
            throw new InvalidOperationException("No se encontró la entrada de horario indicada.");

        var role = await _currentUserService.GetCurrentUserRoleAsync().ConfigureAwait(false);
        var isTeacher = string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(role, "docente", StringComparison.OrdinalIgnoreCase);
        if (isTeacher && entry.TeacherAssignment.TeacherId != currentUserId)
            throw new UnauthorizedAccessException("Solo puede eliminar horarios de sus propias asignaciones.");

        _context.ScheduleEntries.Remove(entry);
        await _context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<List<ScheduleEntry>> GetByTeacherAsync(Guid teacherId, Guid academicYearId)
    {
        return await _context.ScheduleEntries
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(s => s!.Subject)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(s => s!.Group)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.Teacher)
            .Include(e => e.TimeSlot)
            .Include(e => e.AcademicYear)
            .Where(e => e.TeacherAssignment.TeacherId == teacherId && e.AcademicYearId == academicYearId)
            .OrderBy(e => e.DayOfWeek)
            .ThenBy(e => e.TimeSlot.DisplayOrder)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<List<ScheduleEntry>> GetByGroupAsync(Guid groupId, Guid academicYearId)
    {
        return await _context.ScheduleEntries
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(s => s!.Subject)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.SubjectAssignment)
                    .ThenInclude(s => s!.Group)
            .Include(e => e.TeacherAssignment)
                .ThenInclude(t => t!.Teacher)
            .Include(e => e.TimeSlot)
            .Include(e => e.AcademicYear)
            .Where(e =>
                e.TeacherAssignment.SubjectAssignment.GroupId == groupId &&
                e.AcademicYearId == academicYearId)
            .OrderBy(e => e.DayOfWeek)
            .ThenBy(e => e.TimeSlot.DisplayOrder)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<ScheduleEntry>> GetByStudentUserAsync(Guid studentUserId, Guid academicYearId)
    {
        var userSchoolId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == studentUserId)
            .Select(u => u.SchoolId)
            .FirstOrDefaultAsync(CancellationToken.None)
            .ConfigureAwait(false);

        if (userSchoolId == null || userSchoolId == Guid.Empty)
            return new List<ScheduleEntry>();

        var assignments = await _context.StudentAssignments
            .AsNoTracking()
            .Include(sa => sa.Group)
            .Where(sa =>
                sa.StudentId == studentUserId &&
                sa.IsActive &&
                (sa.AcademicYearId == academicYearId || sa.AcademicYearId == null))
            .OrderByDescending(sa => sa.AcademicYearId != null)
            .ThenByDescending(sa => sa.CreatedAt)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);

        if (assignments.Count == 0)
            return new List<ScheduleEntry>();

        var merged = new List<ScheduleEntry>();
        var seenEntryIds = new HashSet<Guid>();
        foreach (var assignment in assignments)
        {
            if (assignment.Group == null || assignment.Group.SchoolId != userSchoolId)
                continue;

            var forGroup = await GetByGroupAsync(assignment.GroupId, academicYearId).ConfigureAwait(false);
            foreach (var entry in forGroup)
            {
                if (seenEntryIds.Add(entry.Id))
                    merged.Add(entry);
            }
        }

        return merged
            .OrderBy(e => e.DayOfWeek)
            .ThenBy(e => e.TimeSlot.DisplayOrder)
            .ToList();
    }

    private static string FormatTimeSlotLabel(TimeSlot timeSlot) =>
        $"{timeSlot.Name} ({timeSlot.StartTime:HH:mm} – {timeSlot.EndTime:HH:mm})";

    private static string SpanishDayName(byte dayOfWeek) => dayOfWeek switch
    {
        1 => "lunes",
        2 => "martes",
        3 => "miércoles",
        4 => "jueves",
        5 => "viernes",
        6 => "sábado",
        7 => "domingo",
        _ => $"día {dayOfWeek}"
    };
}
