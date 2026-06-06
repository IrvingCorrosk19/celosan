using SchoolManager.Models;
using Microsoft.EntityFrameworkCore;
using SchoolManager.ViewModels;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Implementations;


public class TeacherAssignmentService : ITeacherAssignmentService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TeacherAssignmentService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<TeacherAssignment?> GetExistingAssignmentAsync(
        Guid currentTeacherId,
        Guid specialtyId,
        Guid areaId,
        Guid subjectId,
        Guid gradeLevelId,
        Guid groupId)
    {
        return await _context.TeacherAssignments
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Subject)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Group)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.GradeLevel)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Specialty)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Area)
            .Include(ta => ta.Teacher)
            .Where(ta => 
                ta.TeacherId != currentTeacherId && // Diferente profesor
                ta.SubjectAssignment.SpecialtyId == specialtyId &&
                ta.SubjectAssignment.AreaId == areaId &&
                ta.SubjectAssignment.SubjectId == subjectId &&
                ta.SubjectAssignment.GradeLevelId == gradeLevelId &&
                ta.SubjectAssignment.GroupId == groupId)
            .FirstOrDefaultAsync();
    }

    // Elimina todas las asignaciones existentes de un profesor
    public async Task DeleteAllAssignmentsByTeacherIdAsync(Guid teacherId)
    {
        var assignmentIds = await _context.TeacherAssignments
            .Where(ta => ta.TeacherId == teacherId)
            .Select(ta => ta.Id)
            .ToListAsync();

        if (assignmentIds.Count == 0)
            return;

        var scheduleEntries = await _context.ScheduleEntries
            .Where(se => assignmentIds.Contains(se.TeacherAssignmentId))
            .ToListAsync();
        if (scheduleEntries.Count > 0)
            _context.ScheduleEntries.RemoveRange(scheduleEntries);

        var assignments = _context.TeacherAssignments.Where(ta => ta.TeacherId == teacherId);
        _context.TeacherAssignments.RemoveRange(assignments);
        await _context.SaveChangesAsync();
    }

    // Agrega una nueva asignación al profesor dado el SubjectAssignmentId
    public async Task AddAssignmentAsync(Guid teacherId, Guid subjectAssignmentId)
    {
        var newAssignment = new TeacherAssignment
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            SubjectAssignmentId = subjectAssignmentId,
            // Convertir el DateTime en UTC para que encaje con 'timestamp with time zone'
            CreatedAt = DateTime.UtcNow
        };

        _context.TeacherAssignments.Add(newAssignment);
        await _context.SaveChangesAsync();
    }


    public async Task<(bool Success, List<Guid>? SubjectAssignmentIds, AssignmentDto? FailedAssignment)> GetSubjectAssignmentIdsAsync(SaveTeacherAssignmentsRequest request)
    {
        var subjectAssignmentIds = new List<Guid>();

        foreach (var assignment in request.Assignments)
        {
            var subjectAssignment = await _context.SubjectAssignments.FirstOrDefaultAsync(sa =>
                sa.SpecialtyId == assignment.SpecialtyId &&
                sa.AreaId == assignment.AreaId &&
                sa.SubjectId == assignment.SubjectId &&
                sa.GradeLevelId == assignment.GradeLevelId &&
                sa.GroupId == assignment.GroupId
            );

            if (subjectAssignment != null)
            {
                subjectAssignmentIds.Add(subjectAssignment.Id);
            }
            else
            {
                // Retorna el assignment que falló si no existe
                return (false, null, assignment);
            }
        }

        return (true, subjectAssignmentIds, null);
    }

    public async Task<List<TeacherAssignment>> GetAllWithIncludesAsync()
{
    return await _context.TeacherAssignments
        .Include(ta => ta.Teacher)
        .Include(ta => ta.SubjectAssignment)
            .ThenInclude(sa => sa.Subject)
        .Include(ta => ta.SubjectAssignment.Group)
        .Include(ta => ta.SubjectAssignment.GradeLevel)
        .Include(ta => ta.SubjectAssignment.Area)
        .Include(ta => ta.SubjectAssignment.Specialty)
        .ToListAsync();
}
    public async Task<List<TeacherAssignment>> GetAssignmentsForModalByTeacherIdAsync(Guid teacherId)
    {
        return await _context.TeacherAssignments
            .Include(t => t.SubjectAssignment)
                .ThenInclude(sa => sa.Subject)
            .Include(t => t.SubjectAssignment.Area)
            .Include(t => t.SubjectAssignment.Specialty)
            .Include(t => t.SubjectAssignment.GradeLevel)
            .Include(t => t.SubjectAssignment.Group)
            .Where(t => t.TeacherId == teacherId)
            .ToListAsync();
    }

    public async Task<List<TeacherAssignment>> GetByTeacherIdAsync(Guid teacherId)
    {
        var assignments = await _context.TeacherAssignments
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Subject)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Group)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Area)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Specialty)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.GradeLevel)
            .Where(ta => ta.TeacherId == teacherId)
            .ToListAsync();

        // Log para debugging
        System.Diagnostics.Debug.WriteLine($"Found {assignments.Count} assignments for teacher {teacherId}");
        foreach (var assignment in assignments)
        {
            System.Diagnostics.Debug.WriteLine($"Assignment: {assignment.Id}");
            System.Diagnostics.Debug.WriteLine($"- SubjectAssignment: {assignment.SubjectAssignment?.Id}");
            System.Diagnostics.Debug.WriteLine($"- Subject: {assignment.SubjectAssignment?.Subject?.Name}");
            System.Diagnostics.Debug.WriteLine($"- Group: {assignment.SubjectAssignment?.Group?.Name}");
            System.Diagnostics.Debug.WriteLine($"- Grade: {assignment.SubjectAssignment?.GradeLevel?.Name}");
            System.Diagnostics.Debug.WriteLine($"- Area: {assignment.SubjectAssignment?.Area?.Name}");
            System.Diagnostics.Debug.WriteLine($"- Specialty: {assignment.SubjectAssignment?.Specialty?.Name}");
        }

        return assignments;
    }

    public async Task CreateAsync(Guid teacherId, Guid subjectId, Guid groupId, Guid gradeLevelId, Guid areaId, Guid specialtyId)
    {
        var subjectAssignment = await GetOrCreateSubjectAssignment(subjectId, groupId, gradeLevelId, areaId, specialtyId);

        var exists = await _context.TeacherAssignments.AnyAsync(ta =>
            ta.TeacherId == teacherId &&
            ta.SubjectAssignmentId == subjectAssignment.Id);

        if (!exists)
        {
            var newAssignment = new TeacherAssignment
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                SubjectAssignmentId = subjectAssignment.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.TeacherAssignments.Add(newAssignment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateAsync(Guid assignmentId, Guid subjectId, Guid groupId, Guid gradeLevelId, Guid areaId, Guid specialtyId)
    {
        var assignment = await _context.TeacherAssignments.FindAsync(assignmentId);
        if (assignment == null)
            throw new InvalidOperationException("Asignación no encontrada.");

        var subjectAssignment = await GetOrCreateSubjectAssignment(subjectId, groupId, gradeLevelId, areaId, specialtyId);

        assignment.SubjectAssignmentId = subjectAssignment.Id;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid assignmentId)
    {
        var scheduleEntries = await _context.ScheduleEntries
            .Where(se => se.TeacherAssignmentId == assignmentId)
            .ToListAsync();
        if (scheduleEntries.Count > 0)
            _context.ScheduleEntries.RemoveRange(scheduleEntries);

        var assignment = await _context.TeacherAssignments.FindAsync(assignmentId);
        if (assignment != null)
        {
            _context.TeacherAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TeacherAssignment?> GetByIdAsync(Guid id)
    {
        return await _context.TeacherAssignments
            .Include(ta => ta.SubjectAssignment)
            .FirstOrDefaultAsync(ta => ta.Id == id);
    }

    private async Task<SubjectAssignment> GetOrCreateSubjectAssignment(Guid subjectId, Guid groupId, Guid gradeLevelId, Guid areaId, Guid specialtyId)
    {
        var existing = await _context.SubjectAssignments.FirstOrDefaultAsync(sa =>
            sa.SubjectId == subjectId &&
            sa.GroupId == groupId &&
            sa.GradeLevelId == gradeLevelId &&
            sa.AreaId == areaId &&
            sa.SpecialtyId == specialtyId
        );

        if (existing != null)
            return existing;

        var newAssignment = new SubjectAssignment
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            GroupId = groupId,
            GradeLevelId = gradeLevelId,
            AreaId = areaId,
            SpecialtyId = specialtyId,
            CreatedAt = DateTime.UtcNow,
            Status = "Active"
        };

        await AuditHelper.SetAuditFieldsForCreateAsync(newAssignment, _currentUserService);
        await AuditHelper.SetSchoolIdAsync(newAssignment, _currentUserService);

        _context.SubjectAssignments.Add(newAssignment);
        await _context.SaveChangesAsync();

        return newAssignment;
    }
}
