using SchoolManager.Models;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Enums;
using BCrypt.Net;
using SchoolManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManager.Services.Implementations
{
public class UserService : IUserService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UserService> _logger;

    public UserService(SchoolDbContext context, ICurrentUserService currentUserService, ILogger<UserService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<List<User>> GetAllStudentsAsync()
    {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.SchoolId == null)
                return new List<User>();

        return await _context.Users
            .Where(u => u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante")
                .Where(u => u.SchoolId == currentUser.SchoolId)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task UpdateAsync(User user, List<Guid> subjectIds, List<Guid> groupIds)
    {
        try
        {
            Console.WriteLine($"=== USER SERVICE UPDATE ===");
            Console.WriteLine($"Usuario ID: {user.Id}");
            Console.WriteLine($"Nombre: {user.Name}");
            Console.WriteLine($"Email: {user.Email}");
            Console.WriteLine($"Celular Principal: {user.CellphonePrimary}");
            Console.WriteLine($"Celular Secundario: {user.CellphoneSecondary}");
            Console.WriteLine($"Subjects Count: {subjectIds?.Count ?? 0}");
            Console.WriteLine($"Groups Count: {groupIds?.Count ?? 0}");

            Console.WriteLine("Actualizando usuario en contexto...");
            
            // En lugar de usar Update() que puede causar problemas con claves únicas,
            // vamos a actualizar solo los campos específicos
            var existingUser = await _context.Users.FindAsync(user.Id);
            if (existingUser != null)
            {
                existingUser.Name = user.Name;
                existingUser.LastName = user.LastName;
                existingUser.Email = user.Email;
                existingUser.DocumentId = user.DocumentId;
                existingUser.Role = user.Role;
                existingUser.Status = user.Status;
                existingUser.DateOfBirth = user.DateOfBirth;
                existingUser.CellphonePrimary = user.CellphonePrimary;
                existingUser.CellphoneSecondary = user.CellphoneSecondary;
                existingUser.Disciplina = user.Disciplina;
                existingUser.Inclusion = user.Inclusion;
                existingUser.Orientacion = user.Orientacion;
                existingUser.Inclusivo = user.Inclusivo;
                existingUser.PasswordHash = user.PasswordHash;
                existingUser.UpdatedAt = DateTime.UtcNow;
                
                // Actualizar Subjects
                existingUser.Subjects.Clear();
                if (subjectIds.Any())
                {
                    var subjects = await _context.Subjects.Where(s => subjectIds.Contains(s.Id)).ToListAsync();
                    foreach (var subject in subjects)
                    {
                        existingUser.Subjects.Add(subject);
                    }
                }

                // Actualizar Groups
                existingUser.Groups.Clear();
                if (groupIds.Any())
                {
                    var groups = await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
                    foreach (var group in groups)
                    {
                        existingUser.Groups.Add(group);
                    }
                }
            }
            
            Console.WriteLine("Guardando cambios en base de datos...");
            await _context.SaveChangesAsync();
            
            Console.WriteLine("Usuario actualizado exitosamente en UserService");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== ERROR EN USER SERVICE UPDATE ===");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }
    public async Task UpdateAsync(User user, List<Guid> subjectIds, List<Guid> groupIds, List<Guid> gradeLevelIds)
    {
        // Actualizar Subjects
        user.Subjects.Clear();
        if (subjectIds.Any())
        {
            var subjects = await _context.Subjects.Where(s => subjectIds.Contains(s.Id)).ToListAsync();
            foreach (var subject in subjects)
            {
                user.Subjects.Add(subject);
            }
        }


        // Actualizar Groups
        user.Groups.Clear();
        if (groupIds.Any())
        {
            var groups = await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
            foreach (var group in groups)
            {
                user.Groups.Add(group);
            }
        }

        // Actualizar GradeLevels
        user.Grades.Clear();
        if (gradeLevelIds.Any())
        {
            var grades = await _context.GradeLevels.Where(g => gradeLevelIds.Contains(g.Id)).ToListAsync();
            foreach (var grade in grades)
            {
                user.Grades.Add(grade);
            }
        }

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
    public async Task<List<User>> GetAllTeachersAsync()
    {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.SchoolId == null)
                return new List<User>();

        return await _context.Users
            .Where(u => u.Role == "teacher")
                .Where(u => u.SchoolId == currentUser.SchoolId)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }
    public async Task CreateAsync(User user, List<Guid> subjectIds, List<Guid> groupIds)
    {
        try
        {
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                if (currentUser == null || currentUser.SchoolId == null)
                    throw new InvalidOperationException("No se puede crear el usuario porque no hay un usuario actual o no tiene un colegio asignado.");

                // Asignar el SchoolId del usuario actual
                user.SchoolId = currentUser.SchoolId;

            // La contraseña ya viene hasheada desde el controlador, no necesitamos hashearla de nuevo
            
            // Cargar las entidades completas desde la base de datos
            var subjects = await _context.Subjects.Where(s => subjectIds.Contains(s.Id)).ToListAsync();
            var groups = await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync();

            // Asignar las relaciones
            user.Subjects = subjects;
            user.Groups = groups;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Error al crear el usuario y asignar relaciones.", ex);
        }
    }
    public async Task<User?> GetByIdWithRelationsAsync(Guid id)
    {
        try
        {
            Console.WriteLine($"=== GET BY ID WITH RELATIONS ===");
            Console.WriteLine($"Buscando usuario con ID: {id}");
            
            var user = await _context.Users
                .Include(u => u.Subjects)
                .Include(u => u.Groups)
                .Include(u => u.Grades)
                .Include(u => u.SchoolNavigation)
                .FirstOrDefaultAsync(u => u.Id == id);
                
            if (user != null)
            {
                Console.WriteLine($"Usuario encontrado: {user.Name} {user.LastName}");
                Console.WriteLine($"Email: {user.Email}");
                Console.WriteLine($"Celular Principal: {user.CellphonePrimary}");
                Console.WriteLine($"Celular Secundario: {user.CellphoneSecondary}");
                Console.WriteLine($"Subjects: {user.Subjects?.Count ?? 0}");
                Console.WriteLine($"Groups: {user.Groups?.Count ?? 0}");
            }
            else
            {
                Console.WriteLine("Usuario no encontrado");
            }
            
            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== ERROR EN GET BY ID WITH RELATIONS ===");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

        public async Task<List<User>> GetAllAsync()
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.SchoolId == null)
                return new List<User>();

            return await _context.Users
                .Where(u => u.SchoolId == currentUser.SchoolId)
                .ToListAsync();
        }

    public async Task<List<User>> GetAllWithAssignmentsByRoleAsync(string role)
    {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.SchoolId == null)
                return new List<User>();

        return await _context.Users
            .Where(u => u.Role == role)
                .Where(u => u.SchoolId == currentUser.SchoolId)
            .Include(u => u.TeacherAssignments)
                .ThenInclude(ta => ta.SubjectAssignment)
                    .ThenInclude(sa => sa.Subject)
            .Include(u => u.TeacherAssignments)
                .ThenInclude(ta => ta.SubjectAssignment)
                    .ThenInclude(sa => sa.Group)
            .Include(u => u.TeacherAssignments)
                .ThenInclude(ta => ta.SubjectAssignment)
                    .ThenInclude(sa => sa.GradeLevel)
            .Include(u => u.TeacherAssignments)
                .ThenInclude(ta => ta.SubjectAssignment)
                    .ThenInclude(sa => sa.Area)
            .Include(u => u.TeacherAssignments)
                .ThenInclude(ta => ta.SubjectAssignment)
                    .ThenInclude(sa => sa.Specialty)
            .ToListAsync();
    }

        public async Task<List<User>> GetAllWithAssignmentsByRoleSA(string role)
        {
            return await _context.Users
                .Where(u => u.Role == role)                   
                .Include(u => u.TeacherAssignments)
                    .ThenInclude(ta => ta.SubjectAssignment)
                        .ThenInclude(sa => sa.Subject)
                .Include(u => u.TeacherAssignments)
                    .ThenInclude(ta => ta.SubjectAssignment)
                        .ThenInclude(sa => sa.Group)
                .Include(u => u.TeacherAssignments)
                    .ThenInclude(ta => ta.SubjectAssignment)
                        .ThenInclude(sa => sa.GradeLevel)
                .Include(u => u.TeacherAssignments)
                    .ThenInclude(ta => ta.SubjectAssignment)
                        .ThenInclude(sa => sa.Area)
                .Include(u => u.TeacherAssignments)
                    .ThenInclude(ta => ta.SubjectAssignment)
                        .ThenInclude(sa => sa.Specialty)
                .ToListAsync();
        }
        public async Task<User?> GetByIdAsync(Guid id) =>
        await _context.Users.FindAsync(id);

    public async Task CreateAsync(User user, List<Guid> subjectIds, List<Guid> groupIds, List<Guid> gradeLevelIds)
    {
        try
        {
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                if (currentUser == null || currentUser.SchoolId == null)
                    throw new InvalidOperationException("No se puede crear el usuario porque no hay un usuario actual o no tiene un colegio asignado.");

                // Asignar el SchoolId del usuario actual
                user.SchoolId = currentUser.SchoolId;

            // La contraseña ya viene hasheada desde el controlador, no necesitamos hashearla de nuevo
            
            var subjects = await _context.Subjects.Where(s => subjectIds.Contains(s.Id)).ToListAsync();
            var groups = await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
            var grades = await _context.GradeLevels.Where(g => gradeLevelIds.Contains(g.Id)).ToListAsync();

            user.Subjects = subjects;
            user.Groups = groups;
            user.Grades = grades;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Error al crear el usuario y asignar relaciones.", ex);
        }
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }


public async Task DeleteAsync(Guid id)
{
        await using var transaction = await _context.Database.BeginTransactionAsync(); // �� INICIO TRANSACCIÓN

    try
    {
        var user = await _context.Users
            .Include(u => u.Subjects)
            .Include(u => u.Groups)
            .Include(u => u.Grades)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new InvalidOperationException($"No se encontró el usuario con ID: {id}");

        // Validar el rol usando enum
        if (!Enum.TryParse<UserRole>(user.Role, true, out var parsedRole))
            throw new InvalidOperationException($"Rol no válido o no soportado: {user.Role}");

        switch (parsedRole)
        {
            case UserRole.Student:
            case UserRole.Estudiante:
                // MEJORADO: Inactivar asignaciones en lugar de eliminarlas (preserva historial)
                var studentAssignments = await _context.StudentAssignments
                    .Where(sa => sa.StudentId == id && sa.IsActive)
                    .ToListAsync();
                
                if (studentAssignments.Any())
                {
                    foreach (var assignment in studentAssignments)
                    {
                        assignment.IsActive = false;
                        assignment.EndDate = DateTime.UtcNow;
                    }
                    _context.StudentAssignments.UpdateRange(studentAssignments);
                }
                break;

            case UserRole.Teacher:
                var teacherAssignments = await _context.TeacherAssignments
                    .Where(ta => ta.TeacherId == id)
                    .ToListAsync();
                _context.TeacherAssignments.RemoveRange(teacherAssignments);
                break;

            case UserRole.Admin:
            case UserRole.Director:
            case UserRole.Secretaria:
                // No asignaciones específicas
                break;

            default:
                throw new InvalidOperationException($"Rol no manejado: {parsedRole}");
        }

        // Limpieza de relaciones M:M
        //user.Subjects.Clear();
        //user.Groups.Clear();
        //user.Grades.Clear();

        await _context.SaveChangesAsync();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
    }
    catch (DbUpdateException dbEx)
    {
        await transaction.RollbackAsync(); 
        throw new Exception("No se puede eliminar el usuario porque tiene dependencias en otras entidades.", dbEx);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        throw new Exception("Error inesperado al eliminar el usuario.", ex);
    }
    }

    public async Task<User?> GetByRoleAndSchoolAsync(string role, Guid schoolId)
    {
        try
        {
            return await _context.Users
                .Where(u => u.Role.ToLower() == role.ToLower() && u.SchoolId == schoolId && u.Status == "active")
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener usuario por rol {Role} y escuela {SchoolId}", role, schoolId);
            return null;
        }
    }
//public async Task DeleteAsync(Guid id)
//{
//    try
//    {
//        var user = await _context.Users
//            .Include(u => u.Subjects)
//            .Include(u => u.Groups)
//            .Include(u => u.Grades)
//            .FirstOrDefaultAsync(u => u.Id == id);

//        if (user == null)
//            throw new InvalidOperationException($"No se encontró el usuario con ID: {id}");

//        // Eliminar relaciones explícitas
//        //user.Subjects.Clear();
//        //user.Groups.Clear();
//        //user.Grades.Clear();

//        var role = user.Role.ToLower();


//        // Eliminar asignaciones de profesor
//        var assignments = await _context.TeacherAssignments
//            .Where(ta => ta.TeacherId == id)
//            .ToListAsync();

//        _context.TeacherAssignments.RemoveRange(assignments);

//        await _context.SaveChangesAsync();

//        // Eliminar el usuario
//        _context.Users.Remove(user);
//        await _context.SaveChangesAsync();
//    }
//    catch (DbUpdateException dbEx)
//    {
//        throw new Exception("No se puede eliminar el usuario porque tiene dependencias en otras entidades (como asignaciones de docentes).", dbEx);
//    }
//    catch (Exception ex)
//    {
//        throw new Exception("Error inesperado al eliminar el usuario.", ex);
//    }
//}


public async Task<User?> AuthenticateAsync(string email, string password)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == password);
    }
    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower().Trim() == email.ToLower().Trim());
    }

    public async Task<User?> GetByEmailIgnoringTenantAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.ToLower().Trim();
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower().Trim() == normalized);
    }

    public async Task<(bool success, string message)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        Console.WriteLine($"[UserService.ChangePasswordAsync] Iniciando para userId: {userId}");
        Console.WriteLine($"[UserService.ChangePasswordAsync] currentPassword length: {currentPassword?.Length ?? 0}");
        Console.WriteLine($"[UserService.ChangePasswordAsync] newPassword length: {newPassword?.Length ?? 0}");
        
        try
        {
            var user = await _context.Users.FindAsync(userId);
            Console.WriteLine($"[UserService.ChangePasswordAsync] Usuario encontrado: {(user != null ? $"ID={user.Id}, Email={user.Email}" : "NULL")}");
            
            if (user == null)
            {
                Console.WriteLine($"[UserService.ChangePasswordAsync] ERROR: Usuario no encontrado");
                return (false, "Usuario no encontrado");
            }

            Console.WriteLine($"[UserService.ChangePasswordAsync] PasswordHash actual: {user.PasswordHash?.Substring(0, Math.Min(20, user.PasswordHash?.Length ?? 0))}...");
            Console.WriteLine($"[UserService.ChangePasswordAsync] IsPasswordHashed: {IsPasswordHashed(user.PasswordHash)}");

            // Verificar contraseña actual
            bool currentPasswordValid = false;
            if (IsPasswordHashed(user.PasswordHash))
            {
                Console.WriteLine($"[UserService.ChangePasswordAsync] Verificando contraseña hasheada con BCrypt");
                currentPasswordValid = BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash);
            }
            else
            {
                Console.WriteLine($"[UserService.ChangePasswordAsync] Verificando contraseña en texto plano");
                currentPasswordValid = currentPassword == user.PasswordHash;
            }

            Console.WriteLine($"[UserService.ChangePasswordAsync] Contraseña actual válida: {currentPasswordValid}");

            if (!currentPasswordValid)
            {
                Console.WriteLine($"[UserService.ChangePasswordAsync] ERROR: Contraseña actual incorrecta");
                return (false, "La contraseña actual que ingresaste no es correcta. Por favor, verifica e intenta nuevamente.");
            }

            // Validar nueva contraseña
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                Console.WriteLine($"[UserService.ChangePasswordAsync] ERROR: Nueva contraseña inválida (length: {newPassword?.Length ?? 0})");
                return (false, "La nueva contraseña debe tener al menos 8 caracteres");
            }

            // Verificar que la nueva contraseña sea diferente a la actual
            if (currentPassword == newPassword)
            {
                Console.WriteLine($"[UserService.ChangePasswordAsync] ERROR: Nueva contraseña igual a la actual");
                return (false, "La nueva contraseña debe ser diferente a la actual");
            }

            Console.WriteLine($"[UserService.ChangePasswordAsync] Hasheando nueva contraseña...");
            // Hashear y actualizar contraseña
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            Console.WriteLine($"[UserService.ChangePasswordAsync] Guardando cambios en la base de datos...");
            await _context.SaveChangesAsync();
            Console.WriteLine($"[UserService.ChangePasswordAsync] SUCCESS: Contraseña actualizada exitosamente");
            return (true, "Contraseña actualizada exitosamente");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserService.ChangePasswordAsync] EXCEPCIÓN: {ex.Message}");
            Console.WriteLine($"[UserService.ChangePasswordAsync] StackTrace: {ex.StackTrace}");
            return (false, $"Error inesperado al cambiar la contraseña: {ex.Message}");
        }
    }

    private bool IsPasswordHashed(string passwordHash)
    {
        // BCrypt hashes start with $2a$, $2b$, or $2y$
        return passwordHash.StartsWith("$2") && passwordHash.Length > 20;
    }
}
}
