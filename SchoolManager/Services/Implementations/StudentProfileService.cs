using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;

namespace SchoolManager.Services.Implementations
{
    /// <summary>
    /// Implementación del servicio de perfil de estudiantes
    /// </summary>
    public class StudentProfileService : IStudentProfileService
    {
        private static readonly HashSet<string> AllowedBloodTypes = new(StringComparer.Ordinal)
        {
            "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"
        };

        private readonly SchoolDbContext _context;
        private readonly ILogger<StudentProfileService> _logger;

        public StudentProfileService(SchoolDbContext context, ILogger<StudentProfileService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StudentProfileViewModel?> GetStudentProfileAsync(Guid studentId)
        {
            try
            {
                _logger.LogInformation("📋 Obteniendo perfil del estudiante: {StudentId}", studentId);

                var user = await _context.Users
                    .Where(u => u.Id == studentId && (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante"))
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.LastName,
                        u.Email,
                        u.DocumentId,
                        u.DateOfBirth,
                        u.CellphonePrimary,
                        u.CellphoneSecondary,
                        u.Role,
                        u.SchoolId,
                        u.PhotoUrl,
                        u.EmergencyContactName,
                        u.EmergencyContactPhone,
                        u.EmergencyRelationship,
                        u.BloodType,
                        u.Allergies
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("⚠️ Usuario no encontrado o no es estudiante: {StudentId}", studentId);
                    return null;
                }

                // Matrículas activas (multi-contexto nocturno)
                var activeAssignments = await _context.StudentAssignments
                    .AsNoTracking()
                    .Where(sa => sa.StudentId == studentId && sa.IsActive)
                    .Include(sa => sa.Grade)
                    .Include(sa => sa.Group)
                    .Include(sa => sa.Shift)
                    .ToListAsync();

                var primary = ActiveStudentAssignmentHelper.GetPrimaryEnrollment(activeAssignments);
                var ordered = ActiveStudentAssignmentHelper.GetAllActiveOrdered(activeAssignments);

                var enrollmentSummaries = ordered.Select(a => new StudentEnrollmentSummaryDto
                {
                    GradeName = a.Grade?.Name ?? "—",
                    GroupName = a.Group?.Name ?? "—",
                    ShiftName = a.Shift?.Name,
                    EnrollmentType = a.EnrollmentType ?? EnrollmentTypeConstants.Regular,
                    IsPrimary = primary != null && a.Id == primary.Id,
                    IsCarryOver = EnrollmentTypeConstants.IsCarryOver(a.EnrollmentType)
                }).ToList();

                // Obtener nombre de la escuela
                var school = user.SchoolId.HasValue
                    ? await _context.Schools
                        .Where(s => s.Id == user.SchoolId.Value)
                        .Select(s => s.Name)
                        .FirstOrDefaultAsync()
                    : null;

                var profile = new StudentProfileViewModel
                {
                    Id = user.Id,
                    Name = user.Name,
                    LastName = user.LastName,
                    Email = user.Email,
                    DocumentId = user.DocumentId,
                    DateOfBirth = user.DateOfBirth,
                    CellphonePrimary = user.CellphonePrimary,
                    CellphoneSecondary = user.CellphoneSecondary,
                    Role = user.Role,
                    Grade = primary?.Grade?.Name,
                    GroupName = primary?.Group?.Name,
                    AdditionalEnrollmentsSummary = ActiveStudentAssignmentHelper.BuildMultiEnrollmentSummary(activeAssignments, primary),
                    ActiveEnrollments = enrollmentSummaries,
                    SchoolName = school,
                    PhotoUrl = user.PhotoUrl,
                    EmergencyContactName = user.EmergencyContactName,
                    EmergencyContactPhone = user.EmergencyContactPhone,
                    EmergencyRelationship = user.EmergencyRelationship,
                    BloodType = user.BloodType,
                    Allergies = user.Allergies
                };

                _logger.LogInformation("✅ Perfil obtenido correctamente para: {Name} {LastName}", user.Name, user.LastName);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo perfil del estudiante: {StudentId}", studentId);
                throw;
            }
        }

        public async Task<bool> UpdateStudentProfileAsync(StudentProfileViewModel model)
        {
            try
            {
                _logger.LogInformation("📝 Actualizando perfil del estudiante: {StudentId}", model.Id);

                var user = await _context.Users.FindAsync(model.Id);

                if (user == null || (user.Role.ToLower() != "student" && user.Role.ToLower() != "estudiante"))
                {
                    _logger.LogWarning("⚠️ Usuario no encontrado o no es estudiante: {StudentId}", model.Id);
                    return false;
                }

                // Validar que el email no esté en uso
                if (user.Email != model.Email)
                {
                    var emailInUse = await _context.Users
                        .AnyAsync(u => u.Email == model.Email && u.Id != model.Id);

                    if (emailInUse)
                    {
                        _logger.LogWarning("⚠️ El email ya está en uso: {Email}", model.Email);
                        return false;
                    }
                }

                // Validar que el documento de identidad no esté en uso
                if (!string.IsNullOrEmpty(model.DocumentId) && user.DocumentId != model.DocumentId)
                {
                    var documentInUse = await _context.Users
                        .AnyAsync(u => u.DocumentId == model.DocumentId && u.Id != model.Id);

                    if (documentInUse)
                    {
                        _logger.LogWarning("⚠️ El documento de identidad ya está en uso: {DocumentId}", model.DocumentId);
                        return false;
                    }
                }

                // Actualizar solo los campos permitidos (incl. alergias y contacto de emergencia para el carnet)
                user.Name = model.Name;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.DocumentId = model.DocumentId;
                user.DateOfBirth = model.DateOfBirth;
                user.CellphonePrimary = model.CellphonePrimary;
                user.CellphoneSecondary = model.CellphoneSecondary;
                user.BloodType = NormalizeBloodType(model.BloodType);
                user.Allergies = model.Allergies;
                user.EmergencyContactName = model.EmergencyContactName;
                user.EmergencyContactPhone = model.EmergencyContactPhone;
                user.EmergencyRelationship = model.EmergencyRelationship;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = model.Id; // El estudiante se actualiza a sí mismo

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Perfil actualizado correctamente: {Name} {LastName}", user.Name, user.LastName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error actualizando perfil del estudiante: {StudentId}", model.Id);
                throw;
            }
        }

        public async Task<bool> IsEmailAvailableAsync(string email, Guid currentUserId)
        {
            try
            {
                var exists = await _context.Users
                    .AnyAsync(u => u.Email == email && u.Id != currentUserId);

                return !exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validando disponibilidad de email: {Email}", email);
                return false;
            }
        }

        public async Task<bool> IsDocumentIdAvailableAsync(string? documentId, Guid currentUserId)
        {
            if (string.IsNullOrEmpty(documentId))
                return true;

            try
            {
                var exists = await _context.Users
                    .AnyAsync(u => u.DocumentId == documentId && u.Id != currentUserId);

                return !exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validando disponibilidad de documento: {DocumentId}", documentId);
                return false;
            }
        }

        private static string? NormalizeBloodType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var t = value.Trim();
            return AllowedBloodTypes.Contains(t) ? t : null;
        }
    }
}

