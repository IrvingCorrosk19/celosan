using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using System.Security.Cryptography;
using System.Text;

namespace SchoolManager.Services.Implementations;

public class SuperAdminService : ISuperAdminService
{
    private readonly SchoolDbContext _context;
    private readonly ILogger<SuperAdminService> _logger;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IAcademicYearService _academicYearService;

    public SuperAdminService(
        SchoolDbContext context,
        ILogger<SuperAdminService> logger,
        IAcademicYearService academicYearService,
        ICloudinaryService cloudinaryService)
    {
        _context = context;
        _logger = logger;
        _academicYearService = academicYearService;
        _cloudinaryService = cloudinaryService;
    }

    #region Escuelas

    public async Task<List<SchoolListViewModel>> GetAllSchoolsAsync(string? searchString = null)
    {
        Console.WriteLine($"🏫 [SuperAdminService] Obteniendo lista de escuelas con filtro: '{searchString}'");
        
        var query = from s in _context.Schools.IgnoreQueryFilters()
                   join u in _context.Users on s.AdminId equals u.Id into adminJoin
                   from admin in adminJoin.DefaultIfEmpty()
                   select new SchoolListViewModel
                   {
                       SchoolId = s.Id,
                       SchoolName = s.Name,
                       SchoolAddress = s.Address,
                       SchoolPhone = s.Phone,
                       SchoolLogoUrl = s.LogoUrl,
                       IsActive = s.IsActive,
                       AdminId = admin != null ? admin.Id : Guid.Empty,
                       AdminName = admin != null ? admin.Name : "",
                       AdminLastName = admin != null ? admin.LastName : "",
                       AdminEmail = admin != null ? admin.Email : "",
                       AdminStatus = admin != null ? admin.Status : "",
                       CreatedAt = s.CreatedAt
                   };

        if (!string.IsNullOrEmpty(searchString))
        {
            searchString = searchString.ToLower();
            query = query.Where(s => s.SchoolName.ToLower().Contains(searchString) ||
                                   s.AdminName.ToLower().Contains(searchString) ||
                                   s.AdminEmail.ToLower().Contains(searchString));
        }

        var schools = await query.ToListAsync();
        Console.WriteLine($"✅ [SuperAdminService] Encontradas {schools.Count} escuelas");
        
        foreach (var school in schools)
        {
            Console.WriteLine($"   - {school.SchoolName} (ID: {school.SchoolId}) - Admin: {school.AdminName} {school.AdminLastName}");
        }

        return schools;
    }

    public async Task<School?> GetSchoolByIdAsync(Guid id)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Buscando escuela con ID: {id}");
        
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .Include(s => s.Users)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (school != null)
        {
            Console.WriteLine($"✅ [SuperAdminService] Escuela encontrada: {school.Name}");
        }
        else
        {
            Console.WriteLine($"❌ [SuperAdminService] Escuela no encontrada con ID: {id}");
        }

        return school;
    }

    public async Task<SchoolAdminViewModel?> GetSchoolForEditAsync(Guid id)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Obteniendo escuela para edición con ID: {id}");
        
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .Include(s => s.Users)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (school == null)
        {
            Console.WriteLine($"❌ [SuperAdminService] Escuela no encontrada para edición");
            return null;
        }

        var admin = school.Users.FirstOrDefault(u => u.Id == school.AdminId);

        var viewModel = new SchoolAdminViewModel
        {
            SchoolId = school.Id,
            SchoolName = school.Name,
            SchoolAddress = school.Address,
            SchoolPhone = school.Phone,
            PolicyNumber = school.PolicyNumber,
            AdminName = admin?.Name ?? "",
            AdminLastName = admin?.LastName ?? "",
            AdminEmail = admin?.Email ?? "",
            AdminPassword = "",
            AdminStatus = admin?.Status ?? "active"
        };

        Console.WriteLine($"✅ [SuperAdminService] ViewModel creado para escuela: {school.Name}");
        return viewModel;
    }

    public async Task<bool> CreateSchoolWithAdminAsync(SchoolAdminViewModel model, IFormFile? logoFile, string uploadsPath)
    {
        Console.WriteLine($"🏫 [SuperAdminService] Creando escuela con admin: {model.SchoolName}");
        
        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            // Guardar logo si se proporciona
            string? logoUrl = null;
            if (logoFile != null)
            {
                logoUrl = await SaveLogoAsync(logoFile, uploadsPath);
                Console.WriteLine($"📁 [SuperAdminService] Logo guardado: {logoUrl}");
            }

            // Crear la escuela primero sin admin
            var school = new School
            {
                Id = Guid.NewGuid(),
                Name = model.SchoolName,
                Address = model.SchoolAddress,
                Phone = model.SchoolPhone,
                LogoUrl = logoUrl,
                PolicyNumber = string.IsNullOrWhiteSpace(model.PolicyNumber) ? null : model.PolicyNumber.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Schools.Add(school);
            await _context.SaveChangesAsync();
            Console.WriteLine($"🏫 [SuperAdminService] Escuela creada: {school.Name}");

            // Crear el admin con la referencia a la escuela
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Name = model.AdminName,
                LastName = model.AdminLastName,
                Email = model.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.AdminPassword),
                Role = "admin",
                SchoolId = school.Id,
                Status = model.AdminStatus,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();
            Console.WriteLine($"👤 [SuperAdminService] Admin creado: {admin.Name} {admin.LastName}");

            // Actualizar la escuela con la referencia al admin
            school.AdminId = admin.Id;
            _context.Schools.Update(school);
            await _context.SaveChangesAsync();

            // Garantizar que la escuela tenga al menos un año académico activo
            try
            {
                await _academicYearService.EnsureDefaultAcademicYearForSchoolAsync(school.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo crear año académico por defecto para la escuela {SchoolId}. La escuela se creó correctamente.", school.Id);
            }

            // Garantizar 8 bloques horarios por defecto (35 min desde 07:00) si la escuela no tiene ninguno
            try
            {
                await SchoolManager.Scripts.EnsureDefaultTimeSlots.EnsureForSchoolAsync(_context, school.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron crear bloques horarios por defecto para la escuela {SchoolId}. La escuela se creó correctamente.", school.Id);
            }

            await transaction.CommitAsync();

            Console.WriteLine($"✅ [SuperAdminService] Escuela y admin creados exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error creando escuela: {ex.Message}");
            _logger.LogError(ex, "Error creando escuela con admin");
            return false;
        }
    }

    public async Task<bool> UpdateSchoolAsync(SchoolAdminEditViewModel model, IFormFile? logoFile, string uploadsPath)
    {
        Console.WriteLine($"🔄 [SuperAdminService] Actualizando escuela: {model.SchoolName}");
        
        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            var school = await _context.Schools.FindAsync(model.SchoolId);
            if (school == null)
            {
                Console.WriteLine($"❌ [SuperAdminService] Escuela no encontrada para actualizar");
                return false;
            }

            // Actualizar datos de la escuela
            school.Name = model.SchoolName;
            school.Address = model.SchoolAddress;
            school.Phone = model.SchoolPhone;
            school.PolicyNumber = string.IsNullOrWhiteSpace(model.PolicyNumber) ? null : model.PolicyNumber.Trim();

            // Guardar nuevo logo si se proporciona
            if (logoFile != null)
            {
                var logoUrl = await SaveLogoAsync(logoFile, uploadsPath);
                if (!string.IsNullOrEmpty(logoUrl))
                {
                    school.LogoUrl = logoUrl;
                    Console.WriteLine($"📁 [SuperAdminService] Nuevo logo guardado: {logoUrl}");
                }
            }

            // Actualizar admin si se proporciona
            if (!string.IsNullOrEmpty(model.AdminName))
            {
                var admin = await _context.Users.FindAsync(model.AdminId);
                if (admin != null)
                {
                    admin.Name = model.AdminName;
                    admin.LastName = model.AdminLastName;
                    admin.Email = model.AdminEmail;
                    admin.Status = model.AdminStatus;

                    Console.WriteLine($"👤 [SuperAdminService] Admin actualizado: {admin.Name} {admin.LastName}");
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            Console.WriteLine($"✅ [SuperAdminService] Escuela actualizada exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error actualizando escuela: {ex.Message}");
            _logger.LogError(ex, "Error actualizando escuela");
            return false;
        }
    }

    public async Task<bool> DeleteSchoolAsync(Guid id)
    {
        Console.WriteLine($"🗑️ [SuperAdminService] Desactivando escuela (soft delete) con ID: {id}");
        
        try
        {
            var school = await _context.Schools
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (school == null)
            {
                Console.WriteLine($"❌ [SuperAdminService] Escuela no encontrada con ID: {id}");
                return false;
            }

            school.IsActive = false;
            _context.Schools.Update(school);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ [SuperAdminService] Escuela desactivada correctamente: {school.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error desactivando escuela: {ex.Message}");
            _logger.LogError(ex, "Error desactivando escuela");
            return false;
        }
    }

    public async Task<SchoolAdminEditViewModel?> GetSchoolForEditWithAdminAsync(Guid id)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Obteniendo escuela para edición con admin, ID: {id}");
        
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .Include(s => s.Users)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (school == null)
        {
            Console.WriteLine($"❌ [SuperAdminService] Escuela no encontrada para edición");
            return null;
        }

        var admin = school.Users.FirstOrDefault(u => u.Id == school.AdminId);

        var viewModel = new SchoolAdminEditViewModel
        {
            SchoolId = school.Id,
            SchoolName = school.Name,
            SchoolAddress = school.Address,
            SchoolPhone = school.Phone,
            AdminId = admin?.Id ?? Guid.Empty,
            AdminName = admin?.Name ?? "",
            AdminLastName = admin?.LastName ?? "",
            AdminEmail = admin?.Email ?? "",
            AdminStatus = admin?.Status ?? "active"
        };

        Console.WriteLine($"✅ [SuperAdminService] ViewModel creado para escuela: {school.Name}");
        return viewModel;
    }

    #endregion

    #region Usuarios

    public async Task<List<User>> GetAllAdminsAsync()
    {
        Console.WriteLine($"👥 [SuperAdminService] Obteniendo lista de admins");
        
        var admins = await _context.Users
            .Where(u => u.Role == "admin" || u.Role == "superadmin")
            .Include(u => u.SchoolNavigation)
            .ToListAsync();

        Console.WriteLine($"✅ [SuperAdminService] Encontrados {admins.Count} admins");
        return admins;
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Buscando usuario con ID: {id}");
        
        var user = await _context.Users
            .Include(u => u.SchoolNavigation)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user != null)
        {
            Console.WriteLine($"✅ [SuperAdminService] Usuario encontrado: {user.Name} {user.LastName}");
        }
        else
        {
            Console.WriteLine($"❌ [SuperAdminService] Usuario no encontrado con ID: {id}");
        }

        return user;
    }

    public async Task<UserEditViewModel?> GetUserForEditAsync(Guid id)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Obteniendo usuario para edición con ID: {id}");
        
        var user = await _context.Users
            .Include(u => u.SchoolNavigation)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            Console.WriteLine($"❌ [SuperAdminService] Usuario no encontrado para edición");
            return null;
        }

        var viewModel = new UserEditViewModel
        {
            Id = user.Id,
            Name = user.Name,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status,
            PhotoUrl = user.PhotoUrl
        };

        Console.WriteLine($"✅ [SuperAdminService] ViewModel creado para usuario: {user.Name}");
        return viewModel;
    }

    public async Task<bool> UpdateUserAsync(UserEditViewModel model)
    {
        Console.WriteLine($"🔄 [SuperAdminService] Actualizando usuario: {model.Name} {model.LastName}");
        
        try
        {
            var user = await _context.Users.FindAsync(model.Id);
            if (user == null)
            {
                Console.WriteLine($"❌ [SuperAdminService] Usuario no encontrado para actualizar");
                return false;
            }

            // Protección para usuarios superadmin - no pueden ser inactivados
            if (user.Role == "superadmin" && model.Status == "inactive")
            {
                Console.WriteLine($"🚫 [SuperAdminService] Intento de inactivar superadmin bloqueado: {user.Name}");
                return false;
            }

            user.Name = model.Name;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.Role = model.Role;
            
            // Solo actualizar status si no es superadmin o si se está activando
            if (user.Role != "superadmin" || model.Status == "active")
            {
                user.Status = model.Status;
            }
            else
            {
                // Forzar status activo para superadmin
                user.Status = "active";
                Console.WriteLine($"🔒 [SuperAdminService] Status forzado a 'active' para superadmin: {user.Name}");
            }

            Console.WriteLine($"👤 [SuperAdminService] Usuario actualizado: {user.Name} {user.LastName}");

            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ [SuperAdminService] Usuario actualizado exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error actualizando usuario: {ex.Message}");
            _logger.LogError(ex, "Error actualizando usuario");
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        Console.WriteLine($"🗑️ [SuperAdminService] Eliminando usuario con ID: {id}");
        
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                Console.WriteLine($"❌ [SuperAdminService] Usuario no encontrado para eliminar");
                return false;
            }

            // Eliminar relaciones del usuario
            await DeleteUserRelationsAsync(user);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ [SuperAdminService] Usuario eliminado exitosamente: {user.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error eliminando usuario: {ex.Message}");
            _logger.LogError(ex, "Error eliminando usuario");
            return false;
        }
    }

    #endregion

    #region Diagnóstico

    public async Task<object> DiagnoseSchoolAsync(Guid id)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Diagnosticando escuela con ID: {id}");
        
        var school = await _context.Schools
            .IgnoreQueryFilters()
            .Include(s => s.Users)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (school == null)
        {
            return new { error = "Escuela no encontrada" };
        }

        var diagnosis = new
        {
            school = new
            {
                id = school.Id,
                name = school.Name,
                address = school.Address,
                phone = school.Phone,
                logoUrl = school.LogoUrl,
                createdAt = school.CreatedAt
            },
            users = school.Users.Select(u => new
            {
                id = u.Id,
                name = u.Name,
                lastName = u.LastName,
                email = u.Email,
                role = u.Role,
                status = u.Status
            }).ToList(),
            userCount = school.Users.Count
        };

        Console.WriteLine($"✅ [SuperAdminService] Diagnóstico completado para: {school.Name}");
        return diagnosis;
    }

    #endregion

    #region Archivos

    public async Task<string?> SaveLogoAsync(IFormFile? logoFile, string uploadsPath = "")
    {
        if (logoFile == null || logoFile.Length == 0)
            return null;

        try
        {
            if (!_cloudinaryService.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Cloudinary no está configurado. Los logos de escuela solo se guardan en Cloudinary en todos los ambientes. " +
                    "Defina CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY y CLOUDINARY_API_SECRET.");
            }

            Console.WriteLine($"☁️ [SuperAdminService] Subiendo logo a Cloudinary...");
            var logoUrl = await _cloudinaryService.UploadImageAsync(logoFile, "schools/logos");
            if (string.IsNullOrEmpty(logoUrl))
                throw new InvalidOperationException("No se pudo subir el logo a Cloudinary.");

            Console.WriteLine($"✅ [SuperAdminService] Logo guardado en Cloudinary: {logoUrl}");
            return logoUrl;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error guardando logo: {ex.Message}");
            _logger.LogError(ex, "Error guardando logo en Cloudinary");
            return null;
        }
    }

    public async Task<string?> SaveAvatarAsync(IFormFile? avatarFile, string uploadsPath = "")
    {
        if (avatarFile == null || avatarFile.Length == 0)
            return null;

        try
        {
            if (!_cloudinaryService.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Cloudinary no está configurado. Los avatares solo se guardan en Cloudinary en todos los ambientes. " +
                    "Defina CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY y CLOUDINARY_API_SECRET.");
            }

            Console.WriteLine($"☁️ [SuperAdminService] Subiendo avatar a Cloudinary...");
            var avatarUrl = await _cloudinaryService.UploadImageAsync(avatarFile, "users/avatars");
            if (string.IsNullOrEmpty(avatarUrl))
                throw new InvalidOperationException("No se pudo subir el avatar a Cloudinary.");

            Console.WriteLine($"✅ [SuperAdminService] Avatar guardado en Cloudinary: {avatarUrl}");
            return avatarUrl;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error guardando avatar: {ex.Message}");
            _logger.LogError(ex, "Error guardando avatar en Cloudinary");
            return null;
        }
    }

    public async Task<byte[]?> GetLogoAsync(string? logoUrl)
    {
        if (string.IsNullOrEmpty(logoUrl))
            return null;

        try
        {
            // Si es URL de Cloudinary (https://res.cloudinary.com/...), 
            // devolver null para que la vista use la URL directamente
            if (logoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"☁️ [SuperAdminService] Logo en Cloudinary, acceso directo: {logoUrl}");
                return null;
            }

            // Logo local (compatibilidad con logos antiguos)
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            var filePath = Path.Combine(uploadsPath, "schools", logoUrl);

            if (File.Exists(filePath))
            {
                Console.WriteLine($"📁 [SuperAdminService] Logo local encontrado: {logoUrl}");
                return await File.ReadAllBytesAsync(filePath);
            }

            Console.WriteLine($"❌ [SuperAdminService] Logo no encontrado: {logoUrl}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error leyendo logo: {ex.Message}");
            return null;
        }
    }

    public async Task<byte[]?> GetAvatarAsync(string? avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl))
            return null;

        try
        {
            // Si es URL de Cloudinary, devolver null para acceso directo
            if (avatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"☁️ [SuperAdminService] Avatar en Cloudinary, acceso directo: {avatarUrl}");
                return null;
            }

            // Avatar local (compatibilidad)
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            var filePath = Path.Combine(uploadsPath, "avatars", avatarUrl);

            if (File.Exists(filePath))
            {
                Console.WriteLine($"📁 [SuperAdminService] Avatar local encontrado: {avatarUrl}");
                return await File.ReadAllBytesAsync(filePath);
            }

            Console.WriteLine($"❌ [SuperAdminService] Avatar no encontrado: {avatarUrl}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SuperAdminService] Error leyendo avatar: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Métodos privados

    private async Task DeleteUserRelationsAsync(User user)
    {
        Console.WriteLine($"     🔍 [SuperAdminService] Buscando relaciones para usuario: {user.Name}");

        // MEJORADO: Inactivar asignaciones de estudiantes (preserva historial)
        // Nota: Si realmente necesitas eliminar permanentemente, usa DeleteAssignmentsPermanentlyAsync
        var studentAssignments = await _context.StudentAssignments
            .Where(sa => sa.StudentId == user.Id && sa.IsActive)
            .ToListAsync();
        
        if (studentAssignments.Count > 0)
        {
            Console.WriteLine($"     🗑️ [SuperAdminService] Inactivando {studentAssignments.Count} asignaciones de estudiantes");
            foreach (var assignment in studentAssignments)
            {
                assignment.IsActive = false;
                assignment.EndDate = DateTime.UtcNow;
            }
            _context.StudentAssignments.UpdateRange(studentAssignments);
        }

        // Eliminar entradas de horario asociadas a las asignaciones del docente (FK: schedule_entries → teacher_assignments)
        var teacherAssignmentIds = await _context.TeacherAssignments
            .Where(ta => ta.TeacherId == user.Id)
            .Select(ta => ta.Id)
            .ToListAsync();

        if (teacherAssignmentIds.Count > 0)
        {
            var scheduleEntries = await _context.ScheduleEntries
                .Where(se => teacherAssignmentIds.Contains(se.TeacherAssignmentId))
                .ToListAsync();

            if (scheduleEntries.Count > 0)
            {
                Console.WriteLine($"     🗑️ [SuperAdminService] Eliminando {scheduleEntries.Count} entradas de horario del docente");
                _context.ScheduleEntries.RemoveRange(scheduleEntries);
            }
        }

        // Eliminar asignaciones de profesores
        var teacherAssignments = await _context.TeacherAssignments
            .Where(ta => ta.TeacherId == user.Id)
            .ToListAsync();

        if (teacherAssignments.Count > 0)
        {
            Console.WriteLine($"     🗑️ [SuperAdminService] Eliminando {teacherAssignments.Count} asignaciones de profesores");
            _context.TeacherAssignments.RemoveRange(teacherAssignments);
        }

        // Eliminar puntajes de actividades
        var activityScores = await _context.StudentActivityScores
            .Where(sas => sas.StudentId == user.Id)
            .ToListAsync();
        
        if (activityScores.Count > 0)
        {
            Console.WriteLine($"     🗑️ [SuperAdminService] Eliminando {activityScores.Count} puntajes de actividades");
            _context.StudentActivityScores.RemoveRange(activityScores);
        }

        // Eliminar reportes de disciplina
        var disciplineReports = await _context.DisciplineReports
            .Where(dr => dr.StudentId == user.Id || dr.TeacherId == user.Id)
            .ToListAsync();
        
        if (disciplineReports.Count > 0)
        {
            Console.WriteLine($"     🗑️ [SuperAdminService] Eliminando {disciplineReports.Count} reportes de disciplina");
            _context.DisciplineReports.RemoveRange(disciplineReports);
        }

        // Eliminar asistencias
        var attendances = await _context.Attendances
            .Where(a => a.StudentId == user.Id || a.TeacherId == user.Id)
            .ToListAsync();
        
        if (attendances.Count > 0)
        {
            Console.WriteLine($"     🗑️ [SuperAdminService] Eliminando {attendances.Count} asistencias");
            _context.Attendances.RemoveRange(attendances);
        }

        // Eliminar actividades creadas por el usuario
        var activities = await _context.Activities
            .Where(a => a.TeacherId == user.Id)
            .ToListAsync();
        
        if (activities.Count > 0)
        {
            Console.WriteLine($"     🗑️ [SuperAdminService] Eliminando {activities.Count} actividades");
            _context.Activities.RemoveRange(activities);
        }
    }

    private async Task DeleteSchoolEntitiesAsync(School school)
    {
        Console.WriteLine($"🏫 [SuperAdminService] Eliminando entidades de la escuela: {school.Name}");

        // Eliminar actividades
        var activities = await _context.Activities
            .Where(a => a.SchoolId == school.Id)
            .ToListAsync();
        
        if (activities.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {activities.Count} actividades");
            _context.Activities.RemoveRange(activities);
        }

        // Eliminar tipos de actividades (solo los específicos de la escuela, no los globales)
        var activityTypes = await _context.ActivityTypes
            .Where(at => !at.IsGlobal)
            .ToListAsync();
        
        if (activityTypes.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {activityTypes.Count} tipos de actividades específicos");
            _context.ActivityTypes.RemoveRange(activityTypes);
        }

        // Eliminar logs de auditoría
        var auditLogs = await _context.AuditLogs
            .Where(al => al.SchoolId == school.Id)
            .ToListAsync();
        
        if (auditLogs.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {auditLogs.Count} logs de auditoría");
            _context.AuditLogs.RemoveRange(auditLogs);
        }

        // Eliminar asignaciones de materias
        var subjectAssignments = await _context.SubjectAssignments
            .Where(sa => sa.SchoolId == school.Id)
            .ToListAsync();
        
        if (subjectAssignments.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {subjectAssignments.Count} asignaciones de materias");
            _context.SubjectAssignments.RemoveRange(subjectAssignments);
        }

        // Eliminar grupos
        var groups = await _context.Groups
            .Where(g => g.SchoolId == school.Id)
            .ToListAsync();
        
        if (groups.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {groups.Count} grupos");
            _context.Groups.RemoveRange(groups);
        }

        // Eliminar materias
        var subjects = await _context.Subjects
            .Where(s => s.SchoolId == school.Id)
            .ToListAsync();
        
        if (subjects.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {subjects.Count} materias");
            _context.Subjects.RemoveRange(subjects);
        }

        // Eliminar áreas (solo las específicas de la escuela, no las globales)
        var areas = await _context.Areas
            .Where(a => !a.IsGlobal)
            .ToListAsync();
        
        if (areas.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {areas.Count} áreas específicas");
            _context.Areas.RemoveRange(areas);
        }

        // Eliminar trimestres
        var trimesters = await _context.Trimesters
            .Where(t => t.SchoolId == school.Id)
            .ToListAsync();
        
        if (trimesters.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {trimesters.Count} trimestres");
            _context.Trimesters.RemoveRange(trimesters);
        }

        // Eliminar configuraciones de seguridad
        var securitySettings = await _context.SecuritySettings
            .Where(ss => ss.SchoolId == school.Id)
            .ToListAsync();
        
        if (securitySettings.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {securitySettings.Count} configuraciones de seguridad");
            _context.SecuritySettings.RemoveRange(securitySettings);
        }

        // Eliminar estudiantes
        var students = await _context.Students
            .Where(s => s.SchoolId == school.Id)
            .ToListAsync();
        
        if (students.Count > 0)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando {students.Count} estudiantes");
            _context.Students.RemoveRange(students);
        }
    }

    private async Task DeleteManyToManyRelationsAsync(School school)
    {
        Console.WriteLine($"🔍 [SuperAdminService] Eliminando relaciones muchos a muchos");

        var schoolUsers = await _context.Users
            .Where(u => u.SchoolId == school.Id)
            .ToListAsync();

        foreach (var user in schoolUsers)
        {
            Console.WriteLine($"🗑️ [SuperAdminService] Eliminando relaciones para usuario: {user.Name}");

            // Eliminar relaciones user_groups
            var userGroupsCount = await _context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM user_groups WHERE user_id = '{user.Id}'");
            
            if (userGroupsCount > 0)
            {
                Console.WriteLine($"🗑️ [SuperAdminService] Eliminadas {userGroupsCount} relaciones user_groups para {user.Name}");
            }

            // Eliminar relaciones user_subjects
            var userSubjectsCount = await _context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM user_subjects WHERE user_id = '{user.Id}'");
            
            if (userSubjectsCount > 0)
            {
                Console.WriteLine($"🗑️ [SuperAdminService] Eliminadas {userSubjectsCount} relaciones user_subjects para {user.Name}");
            }

            // Eliminar relaciones user_grades
            var userGradesCount = await _context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM user_grades WHERE user_id = '{user.Id}'");
            
            if (userGradesCount > 0)
            {
                Console.WriteLine($"🗑️ [SuperAdminService] Eliminadas {userGradesCount} relaciones user_grades para {user.Name}");
            }
        }
    }

    #endregion

    #region Directorio de estudiantes (SuperAdmin)

    private static IQueryable<StudentAssignment> WhereAssignmentStudentRole(IQueryable<StudentAssignment> q) =>
        q.Where(sa => sa.Student.Role != null && (
            sa.Student.Role == "student" || sa.Student.Role == "Student" ||
            sa.Student.Role == "estudiante" || sa.Student.Role == "Estudiante" ||
            sa.Student.Role == "alumno" || sa.Student.Role == "Alumno"));

    private static IQueryable<User> WhereUserStudentRole(IQueryable<User> q) =>
        q.Where(u => u.Role != null && (
            u.Role == "student" || u.Role == "Student" ||
            u.Role == "estudiante" || u.Role == "Estudiante" ||
            u.Role == "alumno" || u.Role == "Alumno"));

    public async Task<SuperAdminStudentDirectoryPageVm> GetStudentDirectoryPageAsync(SuperAdminStudentDirectoryFilterVm filter)
    {
        filter ??= new SuperAdminStudentDirectoryFilterVm();
        if (filter.Page < 1)
            filter.Page = 1;
        filter.PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 100);

        var page = new SuperAdminStudentDirectoryPageVm { Filter = filter };

        var optionsBase = WhereAssignmentStudentRole(
            _context.StudentAssignments.AsNoTracking().Where(sa => sa.IsActive));
        if (filter.SchoolId.HasValue)
            optionsBase = optionsBase.Where(sa => sa.Student.SchoolId == filter.SchoolId.Value);

        page.SchoolOptions = await _context.Schools.IgnoreQueryFilters()
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
            .ToListAsync();
        page.SchoolOptions.Insert(0, new SelectListItem { Value = "", Text = "Todas las escuelas" });

        // Una sola lectura para armar combos grado / grupo / jornada (menos round-trips).
        var comboFlat = await optionsBase
            .Select(sa => new
            {
                sa.GradeId,
                GradeName = sa.Grade.Name,
                sa.GroupId,
                GroupName = sa.Group.Name,
                sa.ShiftId,
                ShiftName = sa.Shift != null ? sa.Shift.Name : (string?)null
            })
            .ToListAsync();

        page.GradeOptions = comboFlat
            .GroupBy(x => x.GradeId)
            .Select(g => new SelectListItem { Value = g.Key.ToString(), Text = g.First().GradeName })
            .OrderBy(x => x.Text)
            .ToList();
        page.GradeOptions.Insert(0, new SelectListItem { Value = "", Text = "Todos los niveles / grados" });

        page.GroupOptions = comboFlat
            .GroupBy(x => x.GroupId)
            .Select(g => new SelectListItem { Value = g.Key.ToString(), Text = g.First().GroupName })
            .OrderBy(x => x.Text)
            .ToList();
        page.GroupOptions.Insert(0, new SelectListItem { Value = "", Text = "Todos los grupos" });

        page.ShiftOptions = comboFlat
            .Where(x => x.ShiftId != null && x.ShiftName != null)
            .GroupBy(x => x.ShiftId!.Value)
            .Select(g => new SelectListItem { Value = g.Key.ToString(), Text = g.First().ShiftName! })
            .OrderBy(x => x.Text)
            .ToList();
        page.ShiftOptions.Insert(0, new SelectListItem { Value = "", Text = "Todas las jornadas" });

        MarkSelected(page.SchoolOptions, filter.SchoolId);
        MarkSelected(page.GradeOptions, filter.GradeId);
        MarkSelected(page.GroupOptions, filter.GroupId);
        MarkSelected(page.ShiftOptions, filter.ShiftId);

        IQueryable<SuperAdminStudentDirectoryRowVm> rowsQuery;
        if (filter.OnlyWithoutAssignment)
        {
            rowsQuery = BuildOrphanDirectoryRowsQuery(filter);
        }
        else
        {
            var assigned = BuildAssignmentDirectoryRowsQuery(filter);
            var narrowByEnrollment = filter.GradeId.HasValue || filter.GroupId.HasValue || filter.ShiftId.HasValue;
            rowsQuery = narrowByEnrollment
                ? assigned
                : assigned.Concat(BuildOrphanDirectoryRowsQuery(filter));
        }

        var ordered = rowsQuery
            .OrderBy(r => r.SchoolName ?? "")
            .ThenBy(r => r.FullName);

        page.TotalCount = await ordered.CountAsync();
        page.TotalPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)filter.PageSize);

        if (filter.Page > page.TotalPages && page.TotalPages > 0)
        {
            filter.Page = page.TotalPages;
            ordered = rowsQuery
                .OrderBy(r => r.SchoolName ?? "")
                .ThenBy(r => r.FullName);
        }

        page.Rows = await ordered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return page;
    }

    private static void MarkSelected(List<SelectListItem> items, Guid? selectedId)
    {
        if (!selectedId.HasValue) return;
        var s = selectedId.Value.ToString();
        foreach (var o in items)
            o.Selected = o.Value == s;
    }

    private IQueryable<SuperAdminStudentDirectoryRowVm> BuildAssignmentDirectoryRowsQuery(SuperAdminStudentDirectoryFilterVm filter)
    {
        var saQuery = WhereAssignmentStudentRole(
            _context.StudentAssignments.AsNoTracking().Where(sa => sa.IsActive));

        if (filter.SchoolId.HasValue)
            saQuery = saQuery.Where(sa => sa.Student.SchoolId == filter.SchoolId.Value);
        if (filter.GradeId.HasValue)
            saQuery = saQuery.Where(sa => sa.GradeId == filter.GradeId.Value);
        if (filter.GroupId.HasValue)
            saQuery = saQuery.Where(sa => sa.GroupId == filter.GroupId.Value);
        if (filter.ShiftId.HasValue)
            saQuery = saQuery.Where(sa => sa.ShiftId == filter.ShiftId.Value);

        if (filter.UserStatus == "active")
            saQuery = saQuery.Where(sa => sa.Student.Status == "active");
        else if (filter.UserStatus == "inactive")
            saQuery = saQuery.Where(sa => sa.Student.Status != "active" || sa.Student.Status == null);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var p = "%" + filter.Search.Trim() + "%";
            saQuery = saQuery.Where(sa =>
                EF.Functions.ILike(sa.Student.Name, p) ||
                EF.Functions.ILike(sa.Student.LastName, p) ||
                (sa.Student.Email != null && EF.Functions.ILike(sa.Student.Email, p)) ||
                (sa.Student.DocumentId != null && EF.Functions.ILike(sa.Student.DocumentId, p)));
        }

        return saQuery.Select(sa => new SuperAdminStudentDirectoryRowVm
        {
            UserId = sa.StudentId,
            AssignmentId = sa.Id,
            PhotoUrl = sa.Student.PhotoUrl,
            FullName = sa.Student.Name + " " + sa.Student.LastName,
            DocumentId = sa.Student.DocumentId,
            Email = sa.Student.Email ?? "",
            SchoolName = sa.Student.SchoolNavigation != null ? sa.Student.SchoolNavigation.Name : null,
            SchoolId = sa.Student.SchoolId,
            GradeLevelName = sa.Grade.Name,
            GroupName = sa.Group.Name,
            ShiftName = sa.Shift != null ? sa.Shift.Name : null,
            UserShift = sa.Student.Shift,
            Status = sa.Student.Status ?? "",
            HasActiveAssignment = true
        });
    }

    private IQueryable<SuperAdminStudentDirectoryRowVm> BuildOrphanDirectoryRowsQuery(SuperAdminStudentDirectoryFilterVm filter)
    {
        var q = WhereUserStudentRole(_context.Users.AsNoTracking());
        q = q.Where(u => !_context.StudentAssignments.Any(sa => sa.StudentId == u.Id && sa.IsActive));

        if (filter.SchoolId.HasValue)
            q = q.Where(u => u.SchoolId == filter.SchoolId.Value);

        if (filter.UserStatus == "active")
            q = q.Where(u => u.Status == "active");
        else if (filter.UserStatus == "inactive")
            q = q.Where(u => u.Status != "active" || u.Status == null);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var p = "%" + filter.Search.Trim() + "%";
            q = q.Where(u =>
                EF.Functions.ILike(u.Name, p) ||
                EF.Functions.ILike(u.LastName, p) ||
                (u.Email != null && EF.Functions.ILike(u.Email, p)) ||
                (u.DocumentId != null && EF.Functions.ILike(u.DocumentId, p)));
        }

        return q.Select(u => new SuperAdminStudentDirectoryRowVm
        {
            UserId = u.Id,
            AssignmentId = null,
            PhotoUrl = u.PhotoUrl,
            FullName = u.Name + " " + u.LastName,
            DocumentId = u.DocumentId,
            Email = u.Email ?? "",
            SchoolName = u.SchoolNavigation != null ? u.SchoolNavigation.Name : null,
            SchoolId = u.SchoolId,
            GradeLevelName = null,
            GroupName = null,
            ShiftName = null,
            UserShift = u.Shift,
            Status = u.Status ?? "",
            HasActiveAssignment = false
        });
    }

    #endregion

    #region Directorio de personal (SuperAdmin)

    public async Task<SuperAdminStaffDirectoryPageVm> GetStaffDirectoryPageAsync(SuperAdminStaffDirectoryFilterVm filter)
    {
        filter ??= new SuperAdminStaffDirectoryFilterVm();
        if (filter.Page < 1)
            filter.Page = 1;
        filter.PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 100);

        var page = new SuperAdminStaffDirectoryPageVm { Filter = filter };

        var baseQuery = _context.Users.AsNoTracking()
            .Where(u => u.Role != null && StaffInstitutionalProfileAccess.StaffDirectoryAllowlist.Contains(u.Role));

        if (filter.SchoolId.HasValue)
            baseQuery = baseQuery.Where(u => u.SchoolId == filter.SchoolId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var r = filter.Role.Trim();
            baseQuery = baseQuery.Where(u => u.Role == r);
        }

        if (filter.UserStatus == "active")
            baseQuery = baseQuery.Where(u => u.Status == "active");
        else if (filter.UserStatus == "inactive")
            baseQuery = baseQuery.Where(u => u.Status != "active" || u.Status == null);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var p = "%" + filter.Search.Trim() + "%";
            baseQuery = baseQuery.Where(u =>
                EF.Functions.ILike(u.Name, p) ||
                EF.Functions.ILike(u.LastName, p) ||
                (u.Email != null && EF.Functions.ILike(u.Email, p)) ||
                (u.DocumentId != null && EF.Functions.ILike(u.DocumentId, p)));
        }

        page.SchoolOptions = await _context.Schools.IgnoreQueryFilters()
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
            .ToListAsync();
        page.SchoolOptions.Insert(0, new SelectListItem { Value = "", Text = "Todas las escuelas" });

        var roleRows = await _context.Users.AsNoTracking()
            .Where(u => u.Role != null && StaffInstitutionalProfileAccess.StaffDirectoryAllowlist.Contains(u.Role))
            .Select(u => u.Role!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();
        page.RoleOptions = roleRows
            .Select(r => new SelectListItem { Value = r, Text = StaffInstitutionalRoleFilter.FormatRoleDisplay(r) })
            .ToList();
        page.RoleOptions.Insert(0, new SelectListItem { Value = "", Text = "Todos los roles" });

        MarkSelected(page.SchoolOptions, filter.SchoolId);
        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var fr = filter.Role.Trim();
            foreach (var o in page.RoleOptions)
                o.Selected = o.Value == fr;
        }

        var rowsQuery = baseQuery.Select(u => new SuperAdminStaffDirectoryRowVm
        {
            UserId = u.Id,
            PhotoUrl = u.PhotoUrl,
            FullName = u.Name + " " + u.LastName,
            DocumentId = u.DocumentId,
            Email = u.Email ?? "",
            SchoolName = u.SchoolNavigation != null ? u.SchoolNavigation.Name : null,
            SchoolId = u.SchoolId,
            RoleRaw = u.Role ?? "",
            RoleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(u.Role),
            JobTitle = _context.Set<StaffInstitutionalProfile>()
                .Where(p => p.UserId == u.Id).Select(p => p.JobTitle).FirstOrDefault(),
            Department = _context.Set<StaffInstitutionalProfile>()
                .Where(p => p.UserId == u.Id).Select(p => p.Department).FirstOrDefault(),
            EmployeeCode = _context.Set<StaffInstitutionalProfile>()
                .Where(p => p.UserId == u.Id).Select(p => p.EmployeeCode).FirstOrDefault(),
            Status = u.Status ?? ""
        });

        var ordered = rowsQuery.OrderBy(r => r.SchoolName ?? "").ThenBy(r => r.FullName);

        page.TotalCount = await ordered.CountAsync();
        page.TotalPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)filter.PageSize);

        if (filter.Page > page.TotalPages && page.TotalPages > 0)
            filter.Page = page.TotalPages;

        page.Rows = await ordered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return page;
    }

    #endregion

    #region Estadísticas y Logs

    public async Task<SystemStatsViewModel> GetSystemStatsAsync()
    {
        try
        {
            var stats = new SystemStatsViewModel
            {
                TotalEscuelas = await _context.Schools.CountAsync(),
                TotalUsuarios = await _context.Users.CountAsync(),
                TotalAdmins = await _context.Users.CountAsync(u => u.Role == "admin" || u.Role == "Admin" || u.Role == "director" || u.Role == "Director"),
                TotalProfesores = await _context.Users.CountAsync(u => u.Role == "teacher" || u.Role == "Teacher"),
                TotalEstudiantes = await _context.Users.CountAsync(u => u.Role == "student" || u.Role == "Student" || u.Role == "estudiante" || u.Role == "Estudiante"),
                TotalActividades = await _context.Activities.CountAsync(),
                TotalCalificaciones = await _context.StudentActivityScores.CountAsync(),
                TotalMensajes = await _context.Messages.CountAsync(),
                UsuariosActivos = await _context.Users.CountAsync(u => u.Status == "active"),
                UsuariosInactivos = await _context.Users.CountAsync(u => u.Status != "active"),
                FechaUltimaActividad = (await _context.AuditLogs.MaxAsync(a => (DateTime?)a.Timestamp)) ?? DateTime.UtcNow
            };

            // Estadísticas por escuela
            stats.EscuelasStats = await _context.Schools
                .Select(s => new EscuelaStatsDto
                {
                    Id = s.Id,
                    Nombre = s.Name,
                    LogoUrl = s.LogoUrl,
                    TotalUsuarios = _context.Users.Count(u => u.SchoolId == s.Id),
                    TotalEstudiantes = _context.Users.Count(u => u.SchoolId == s.Id && (u.Role == "student" || u.Role == "Student" || u.Role == "estudiante")),
                    TotalProfesores = _context.Users.Count(u => u.SchoolId == s.Id && (u.Role == "teacher" || u.Role == "Teacher")),
                    TotalActividades = _context.Activities.Count(a => a.SchoolId == s.Id)
                })
                .ToListAsync();

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas del sistema");
            return new SystemStatsViewModel();
        }
    }

    public async Task<PagedResult<AuditLogViewModel>> GetActivityLogsAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var totalLogs = await _context.AuditLogs.CountAsync();
            
            var logs = await _context.AuditLogs
                .Include(a => a.User)
                .Include(a => a.School)
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogViewModel
                {
                    Id = a.Id,
                    UserName = a.UserName ?? "Sistema",
                    UserRole = a.UserRole ?? "N/A",
                    Action = a.Action ?? "N/A",
                    Resource = a.Resource ?? "N/A",
                    Details = a.Details,
                    Timestamp = a.Timestamp,
                    IpAddress = a.IpAddress,
                    SchoolName = a.School != null ? a.School.Name : "N/A"
                })
                .ToListAsync();

            return new PagedResult<AuditLogViewModel>
            {
                Items = logs,
                TotalCount = totalLogs,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalLogs / (double)pageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo logs de actividad");
            return new PagedResult<AuditLogViewModel>
            {
                Items = new List<AuditLogViewModel>(),
                TotalCount = 0,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = 0
            };
        }
    }

    #endregion
} 