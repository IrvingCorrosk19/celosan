using SchoolManager.Models;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations
{
    public class ShiftService : IShiftService
    {
        private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ShiftService(SchoolDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<Shift>> GetAllAsync()
        {
            return await _context.Shifts
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        // Método para obtener todas las jornadas (incluyendo inactivas si es necesario)
        public async Task<List<Shift>> GetAllIncludingInactiveAsync()
        {
            return await _context.Shifts
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Shift?> GetByIdAsync(Guid id)
        {
            return await _context.Shifts.FindAsync(id);
        }

        public async Task<Shift?> GetByNameAsync(string name)
        {
            return await _context.Shifts
                .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
        }

        public async Task<Shift> GetOrCreateAsync(string name)
        {
            name = name.Trim();
            var schoolId = (await _currentUserService.GetCurrentUserAsync())?.SchoolId;
            if (schoolId.HasValue && schoolId.Value != Guid.Empty)
                return await GetOrCreateBySchoolAndNameAsync(schoolId.Value, name);

            var shift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
            
            if (shift == null)
            {
                shift = new Shift
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    IsActive = true,
                    DisplayOrder = 0
                };
                
                // Configurar campos de auditoría y SchoolId
                await AuditHelper.SetAuditFieldsForCreateAsync(shift, _currentUserService);
                await AuditHelper.SetSchoolIdAsync(shift, _currentUserService);
                
                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();
            }
            return shift;
        }

        public async Task<Shift> GetOrCreateBySchoolAndNameAsync(Guid schoolId, string name)
        {
            name = name.Trim();
            var shift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.Name.ToLower() == name.ToLower());

            if (shift == null)
            {
                shift = new Shift
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    Name = name,
                    IsActive = true,
                    DisplayOrder = 0
                };
                await AuditHelper.SetAuditFieldsForCreateAsync(shift, _currentUserService);
                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();
            }
            return shift;
        }

        public async Task<Shift> CreateAsync(Shift shift)
        {
            try
            {
                shift.Id = Guid.NewGuid();
                
                // Configurar campos de auditoría y SchoolId
                await AuditHelper.SetAuditFieldsForCreateAsync(shift, _currentUserService);
                await AuditHelper.SetSchoolIdAsync(shift, _currentUserService);

                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();

                return shift;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al crear la jornada: {ex.Message}", ex);
            }
        }

        public async Task UpdateAsync(Shift shift)
        {
            // Configurar campos de auditoría para actualización
            await AuditHelper.SetAuditFieldsForUpdateAsync(shift, _currentUserService);
            
            _context.Shifts.Update(shift);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            // Validar si la jornada está en uso en algún grupo
            bool enUso = await _context.Groups.AnyAsync(g => g.ShiftId == id);
            if (enUso)
                throw new InvalidOperationException("No se puede borrar la jornada porque está siendo utilizada en grupos. Elimina o reasigna esos grupos primero.");
            
            try
            {
                var shift = await _context.Shifts.FindAsync(id);
                if (shift != null)
                {
                    // En lugar de eliminar, marcar como inactiva
                    shift.IsActive = false;
                    await AuditHelper.SetAuditFieldsForUpdateAsync(shift, _currentUserService);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al eliminar la jornada.", ex);
            }
        }
    }
}

