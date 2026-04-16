using SchoolManager.Models;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations
{
public class SubjectService : ISubjectService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubjectService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }


    public async Task<Subject?> GetByCodeAsync(string code)
    {
        return await _context.Subjects.FirstOrDefaultAsync(s => s.Code.ToLower() == code.ToLower());
    }

    public async Task<List<SubjectAssignment>> GetSubjectAssignmentsByGradeAndGroupAsync(Guid gradeId, Guid groupId)
    {
        return await _context.SubjectAssignments
            .Include(sa => sa.Subject)
            .Include(sa => sa.GradeLevel)
            .Include(sa => sa.Group)
            .Include(sa => sa.Area)
            .Include(sa => sa.Specialty)
            .Where(sa => sa.GradeLevelId == gradeId && sa.GroupId == groupId)
            .ToListAsync();
    }

    public async Task<List<SubjectAssignment>> GetSubjectAssignmentsByStudentAsync(Guid studentId)
    {
        var subjectAssignmentIds = await _context.StudentSubjectAssignments
            .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
            .Select(ssa => ssa.SubjectAssignmentId)
            .Distinct()
            .ToListAsync();

        return await _context.SubjectAssignments
            .Include(sa => sa.Subject)
            .Include(sa => sa.GradeLevel)
            .Include(sa => sa.Group)
            .Include(sa => sa.Area)
            .Include(sa => sa.Specialty)
            .Where(sa => subjectAssignmentIds.Contains(sa.Id))
            .ToListAsync();
    }


    public async Task<Subject> GetOrCreateAsync(string name)
    {
        name = name.Trim().ToUpper();
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Name.ToUpper() == name);
        if (subject == null)
        {
            subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = name,
                Status = true  // ✅ ACTIVA por defecto
            };
            
            // Configurar campos de auditoría y SchoolId
            await AuditHelper.SetAuditFieldsForCreateAsync(subject, _currentUserService);
            await AuditHelper.SetSchoolIdAsync(subject, _currentUserService);
            
            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();
        }
        return subject;
    }

    public async Task<List<Subject>> GetAllAsync()
    {    

        return await _context.Subjects.ToListAsync();
    }

    public async Task<Subject?> GetByIdAsync(Guid id) =>
        await _context.Subjects.FindAsync(id);

    public async Task<Subject> CreateAsync(Subject subject)
    {
        // Configurar campos de auditoría y SchoolId
        await AuditHelper.SetAuditFieldsForCreateAsync(subject, _currentUserService);
        await AuditHelper.SetSchoolIdAsync(subject, _currentUserService);
        
        // guardar en la base de datos
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
        return subject;
    }

    public async Task<Subject> UpdateAsync(Subject subject)
    {
        // Configurar campos de auditoría para actualización
        await AuditHelper.SetAuditFieldsForUpdateAsync(subject, _currentUserService);
        
        _context.Subjects.Update(subject);
        await _context.SaveChangesAsync();
        return subject;
    }


    public async Task DeleteAsync(Guid id)
    {
        // Validar si la materia está en uso en alguna asignación de materia
        bool enUso = await _context.SubjectAssignments.AnyAsync(sa => sa.SubjectId == id);
        if (enUso)
            throw new InvalidOperationException("No se puede borrar la materia porque está siendo utilizada en el catálogo de materias. Elimina o reasigna esas asignaciones primero.");
        try
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject != null)
            {
                _context.Subjects.Remove(subject);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error al eliminar la materia.", ex);
        }
    }

}
}
