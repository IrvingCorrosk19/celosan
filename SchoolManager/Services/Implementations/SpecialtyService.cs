using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SchoolManager.Services.Implementations
{
    public class SpecialtyService : ISpecialtyService
    {
        private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public SpecialtyService(SchoolDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Specialty> GetOrCreateAsync(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("El nombre de la especialidad no puede estar vacío.", nameof(name));

                name = name.Trim().ToUpper();
                var schoolId = (await _currentUserService.GetCurrentUserAsync())?.SchoolId;
                var query = _context.Specialties.Where(e => e.Name.ToUpper() == name);
                if (schoolId.HasValue && schoolId.Value != Guid.Empty)
                    query = query.Where(e => e.SchoolId == schoolId.Value);

                var specialty = await query.FirstOrDefaultAsync();

                if (specialty == null)
                {
                    specialty = new Specialty
                    {
                        Id = Guid.NewGuid(),
                        Name = name
                    };
                    
                    // Configurar campos de auditoría y SchoolId
                    await AuditHelper.SetAuditFieldsForCreateAsync(specialty, _currentUserService);
                    await AuditHelper.SetSchoolIdAsync(specialty, _currentUserService);
                    
                    _context.Specialties.Add(specialty);
                    await _context.SaveChangesAsync();
                }

                return specialty;
            }
            catch (DbUpdateException ex)
            {
                throw new Exception($"No se pudo guardar la especialidad: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al procesar la especialidad: {ex.Message}", ex);
            }
        }

        public async Task<List<Specialty>> GetAllAsync()
        {
            return await _context.Specialties.ToListAsync();
        }

        public async Task<Specialty?> GetByIdAsync(Guid id)
        {
            return await _context.Specialties.FindAsync(id);
        }

        public async Task<Specialty> CreateAsync(Specialty specialty)
        {
            if (specialty == null || string.IsNullOrWhiteSpace(specialty.Name))
                throw new ArgumentException("La especialidad no es válida.");

            specialty.Id = Guid.NewGuid();
            specialty.Name = specialty.Name.Trim();
            specialty.Description = specialty.Description?.Trim();

            // Configurar campos de auditoría y SchoolId
            await AuditHelper.SetAuditFieldsForCreateAsync(specialty, _currentUserService);
            await AuditHelper.SetSchoolIdAsync(specialty, _currentUserService);

            _context.Specialties.Add(specialty);
            await _context.SaveChangesAsync();

            return specialty;
        }

        public async Task<Specialty> UpdateAsync(Specialty specialty)
        {
            if (specialty == null || string.IsNullOrWhiteSpace(specialty.Name))
                throw new ArgumentException("La especialidad no es válida.");

            var existing = await _context.Specialties.FindAsync(specialty.Id);
            if (existing == null)
                throw new InvalidOperationException("Especialidad no encontrada.");

            existing.Name = specialty.Name.Trim();
            existing.Description = specialty.Description?.Trim();

            // Configurar campos de auditoría para actualización
            await AuditHelper.SetAuditFieldsForUpdateAsync(existing, _currentUserService);

            await _context.SaveChangesAsync();

            return existing;
        }

        public async Task DeleteAsync(Guid id)
        {
            // Validar si la especialidad está en uso en alguna asignación de materia
            bool enUso = await _context.SubjectAssignments.AnyAsync(sa => sa.SpecialtyId == id);
            if (enUso)
                throw new InvalidOperationException("No se puede borrar la especialidad porque está siendo utilizada en el catálogo de materias. Elimina o reasigna esas asignaciones primero.");
            var specialty = await _context.Specialties.FindAsync(id);
            if (specialty != null)
            {
                _context.Specialties.Remove(specialty);
                await _context.SaveChangesAsync();
            }
        }
    }
}
