using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SchoolManager.Services.Implementations
{
public class GradeLevelService : IGradeLevelService
{
    private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GradeLevelService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
            _currentUserService = currentUserService;
    }
    public async Task<GradeLevel?> GetByNameAsync(string name)
    {
        var schoolId = (await _currentUserService.GetCurrentUserAsync())?.SchoolId;
        var query = _context.GradeLevels.Where(g => g.Name.ToLower() == name.ToLower());
        if (schoolId.HasValue && schoolId.Value != Guid.Empty)
            query = query.Where(g => g.SchoolId == schoolId.Value);

        return await query.FirstOrDefaultAsync();
    }
    public async Task<GradeLevel> GetOrCreateAsync(string name)
    {
        name = name.Trim().ToUpper();
        var schoolId = (await _currentUserService.GetCurrentUserAsync())?.SchoolId;
        var query = _context.GradeLevels.Where(g => g.Name.ToUpper() == name);
        if (schoolId.HasValue && schoolId.Value != Guid.Empty)
            query = query.Where(g => g.SchoolId == schoolId.Value);

        var grade = await query.FirstOrDefaultAsync();
        if (grade == null)
        {
            grade = new GradeLevel
            {
                Id = Guid.NewGuid(),
                Name = name
            };
            
            // Configurar campos de auditoría y SchoolId
            await AuditHelper.SetAuditFieldsForCreateAsync(grade, _currentUserService);
            await AuditHelper.SetSchoolIdAsync(grade, _currentUserService);
            
            _context.GradeLevels.Add(grade);
            await _context.SaveChangesAsync();
        }
        return grade;
    }

    public async Task<IEnumerable<GradeLevel>> GetAllAsync()
    {
        return await _context.GradeLevels.ToListAsync();
    }

    public async Task<GradeLevel?> GetByIdAsync(Guid id)
    {
        return await _context.GradeLevels.FindAsync(id);
    }

    public async Task<GradeLevel> CreateAsync(GradeLevel gradeLevel)
    {
        try
        {
            gradeLevel.Id = Guid.NewGuid();
            
            // Configurar campos de auditoría y SchoolId
            await AuditHelper.SetAuditFieldsForCreateAsync(gradeLevel, _currentUserService);
            await AuditHelper.SetSchoolIdAsync(gradeLevel, _currentUserService);
            
            _context.GradeLevels.Add(gradeLevel);
            await _context.SaveChangesAsync();
            return gradeLevel;
        }
        catch (Exception ex)
        {
            throw new Exception("Error al crear el grado académico", ex);
        }
    }

    public async Task<GradeLevel> UpdateAsync(GradeLevel gradeLevel)
    {
        try
        {
            // Configurar campos de auditoría para actualización
            await AuditHelper.SetAuditFieldsForUpdateAsync(gradeLevel, _currentUserService);
            
            _context.GradeLevels.Update(gradeLevel);
            await _context.SaveChangesAsync();
            return gradeLevel;
        }
        catch (Exception ex)
        {
            throw new Exception("Error al actualizar el grado académico", ex);
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // Validar si el grado está en uso en alguna asignación de materia
        bool enUso = await _context.SubjectAssignments.AnyAsync(sa => sa.GradeLevelId == id);
        if (enUso)
            throw new InvalidOperationException("No se puede borrar el grado porque está siendo utilizado en el catálogo de materias. Elimina o reasigna esas asignaciones primero.");
        try
        {
            var entity = await _context.GradeLevels.FindAsync(id);
            if (entity == null) return false;

            _context.GradeLevels.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception("Error al eliminar el grado académico", ex);
        }
    }
    }
}
