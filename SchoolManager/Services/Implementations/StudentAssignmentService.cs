using Microsoft.EntityFrameworkCore;

using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManager.Services.Implementations
{
    public class StudentAssignmentService : IStudentAssignmentService
    {
        private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAcademicYearService _academicYearService;

        public StudentAssignmentService(SchoolDbContext context, ICurrentUserService currentUserService, IAcademicYearService academicYearService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _academicYearService = academicYearService;
        }

        private async Task SyncStudentSubjectAssignmentsAsync(StudentAssignment assignment, Guid? explicitSubjectId = null)
        {
            var subjectAssignmentsQuery = _context.SubjectAssignments
                .Where(sa => sa.GradeLevelId == assignment.GradeId && sa.GroupId == assignment.GroupId);

            if (explicitSubjectId.HasValue && explicitSubjectId.Value != Guid.Empty)
                subjectAssignmentsQuery = subjectAssignmentsQuery.Where(sa => sa.SubjectId == explicitSubjectId.Value);

            var subjectAssignments = await subjectAssignmentsQuery
                .Select(sa => new { sa.Id, sa.SubjectId })
                .ToListAsync();

            foreach (var subjectAssignment in subjectAssignments)
            {
                var exists = await _context.StudentSubjectAssignments.AnyAsync(ssa =>
                    ssa.StudentId == assignment.StudentId &&
                    ssa.SubjectAssignmentId == subjectAssignment.Id &&
                    ssa.IsActive &&
                    ssa.AcademicYearId == assignment.AcademicYearId);

                if (exists)
                    continue;

                var enrollment = new StudentSubjectAssignment
                {
                    Id = Guid.NewGuid(),
                    StudentId = assignment.StudentId,
                    SubjectAssignmentId = subjectAssignment.Id,
                    StudentAssignmentId = assignment.Id,
                    AcademicYearId = assignment.AcademicYearId,
                    ShiftId = assignment.ShiftId,
                    EnrollmentType = string.IsNullOrWhiteSpace(assignment.EnrollmentType) ? "Regular" : assignment.EnrollmentType,
                    Status = "Active",
                    IsActive = true,
                    StartDate = assignment.StartDate ?? assignment.CreatedAt ?? DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                await AuditHelper.SetAuditFieldsForCreateAsync(enrollment, _currentUserService);
                await AuditHelper.SetSchoolIdAsync(enrollment, _currentUserService);
                _context.StudentSubjectAssignments.Add(enrollment);
            }
        }
        public async Task InsertAsync(StudentAssignment assignment)
        {
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment), "La asignación no puede ser null.");

            try
            {
                Console.WriteLine($"[StudentAssignmentService] Iniciando inserción para StudentId: {assignment.StudentId}, GradeId: {assignment.GradeId}, GroupId: {assignment.GroupId}");
                
                // Asegurar que CreatedAt esté establecido si no lo está
                if (!assignment.CreatedAt.HasValue)
                {
                    assignment.CreatedAt = DateTime.UtcNow;
                    Console.WriteLine($"[StudentAssignmentService] CreatedAt establecido: {assignment.CreatedAt}");
                }

                // MEJORADO: Asignar año académico si no está asignado
                if (!assignment.AcademicYearId.HasValue)
                {
                    var student = await _context.Users.FindAsync(assignment.StudentId);
                    if (student?.SchoolId.HasValue == true)
                    {
                        var activeAcademicYear = await _academicYearService.GetActiveAcademicYearAsync(student.SchoolId.Value);
                        assignment.AcademicYearId = activeAcademicYear?.Id;
                        Console.WriteLine($"[StudentAssignmentService] AcademicYearId asignado: {assignment.AcademicYearId}");
                    }
                }

                // Asegurar que IsActive esté en true si no está establecido
                if (!assignment.IsActive && !assignment.EndDate.HasValue)
                {
                    assignment.IsActive = true;
                }

                if (string.IsNullOrWhiteSpace(assignment.EnrollmentType))
                    assignment.EnrollmentType = "Regular";
                assignment.StartDate ??= assignment.CreatedAt ?? DateTime.UtcNow;
                
                _context.StudentAssignments.Add(assignment);
                Console.WriteLine($"[StudentAssignmentService] Entidad agregada al contexto");

                await _context.SaveChangesAsync();
                await SyncStudentSubjectAssignmentsAsync(assignment);
                await _context.SaveChangesAsync();
                Console.WriteLine($"[StudentAssignmentService] SaveChangesAsync completado exitosamente");
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"[StudentAssignmentService] DbUpdateException: {dbEx.Message}");
                Console.WriteLine($"[StudentAssignmentService] Inner Exception: {dbEx.InnerException?.Message}");
                // Excepción típica de clave foránea, clave primaria duplicada, etc.
                throw new InvalidOperationException($"Error al guardar la asignación en la base de datos. Verifica claves foráneas y datos duplicados. Detalles: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentAssignmentService] Exception general: {ex.Message}");
                Console.WriteLine($"[StudentAssignmentService] Stack Trace: {ex.StackTrace}");
                // Otro tipo de excepción general
                throw new Exception($"Ocurrió un error inesperado al insertar la asignación. Detalles: {ex.Message}", ex);
            }
        }


        public async Task<bool> ExistsAsync(Guid studentId, Guid gradeId, Guid groupId)
        {
            if (studentId == Guid.Empty || gradeId == Guid.Empty || groupId == Guid.Empty)
                return false;

            // Verificar solo asignaciones activas
            return await _context.StudentAssignments.AnyAsync(sa =>
                sa.StudentId == studentId &&
                sa.GradeId == gradeId &&
                sa.GroupId == groupId &&
                sa.IsActive);
        }

        public async Task<bool> ExistsWithShiftAsync(Guid studentId, Guid gradeId, Guid groupId, Guid? shiftId)
        {
            if (studentId == Guid.Empty || gradeId == Guid.Empty || groupId == Guid.Empty)
                return false;

            return await _context.StudentAssignments.AnyAsync(sa =>
                sa.StudentId == studentId &&
                sa.GradeId == gradeId &&
                sa.GroupId == groupId &&
                sa.IsActive &&
                sa.ShiftId == shiftId);
        }


        public async Task<List<StudentAssignment>> GetAssignmentsByStudentIdAsync(Guid studentId, bool activeOnly = true)
        {
            var query = _context.StudentAssignments
                .Where(sa => sa.StudentId == studentId);
            
            // Por defecto, solo obtener asignaciones activas (para uso normal)
            // Si activeOnly = false, obtener todas incluyendo historial
            if (activeOnly)
            {
                query = query.Where(sa => sa.IsActive);
            }
            
                return await query
                .Select(sa => new StudentAssignment
                {
                    Id = sa.Id,
                    StudentId = sa.StudentId,
                    GradeId = sa.GradeId,
                    GroupId = sa.GroupId,
                    ShiftId = sa.ShiftId,
                    IsActive = sa.IsActive,
                    EndDate = sa.EndDate,
                    CreatedAt = sa.CreatedAt,
                    AcademicYearId = sa.AcademicYearId,
                    EnrollmentType = sa.EnrollmentType,
                    StartDate = sa.StartDate
                })
                .OrderByDescending(sa => sa.CreatedAt) // Más recientes primero
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, List<StudentAssignment>>> GetActiveAssignmentsForCurrentSchoolAsync()
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser?.SchoolId == null)
                return new Dictionary<Guid, List<StudentAssignment>>();

            var schoolId = currentUser.SchoolId.Value;

            // JOIN por escuela: el planificador usa ix_users_school_id_lower_role + ix_student_assignments_active_student_created_at
            // (evita WHERE student_id IN (~1800 valores) que encarece parseo y plan).
            var rows = await (
                from sa in _context.StudentAssignments.AsNoTracking()
                join u in _context.Users.AsNoTracking() on sa.StudentId equals u.Id
                where sa.IsActive
                    && u.SchoolId == schoolId
                    && (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante")
                orderby sa.StudentId, sa.CreatedAt descending
                select new StudentAssignment
                {
                    Id = sa.Id,
                    StudentId = sa.StudentId,
                    GradeId = sa.GradeId,
                    GroupId = sa.GroupId,
                    ShiftId = sa.ShiftId,
                    IsActive = sa.IsActive,
                    EndDate = sa.EndDate,
                    CreatedAt = sa.CreatedAt,
                    AcademicYearId = sa.AcademicYearId,
                    EnrollmentType = sa.EnrollmentType,
                    StartDate = sa.StartDate
                }).ToListAsync();

            return rows
                .GroupBy(sa => sa.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public async Task AssignAsync(Guid studentId, List<(Guid SubjectId, Guid GradeId, Guid GroupId)> assignments, bool replaceExistingActive = true)
        {
            try
            {
                Console.WriteLine($"[StudentAssignmentService] Iniciando AssignAsync para StudentId: {studentId}");
                
                if (replaceExistingActive)
                {
                    // Inactivar todas las asignaciones activas (comportamiento histórico de “reemplazar matrícula”)
                    var existing = await _context.StudentAssignments
                        .Where(a => a.StudentId == studentId && a.IsActive)
                        .ToListAsync();

                    Console.WriteLine($"[StudentAssignmentService] Encontradas {existing.Count} asignaciones activas existentes");

                    foreach (var assignment in existing)
                    {
                        assignment.IsActive = false;
                        assignment.EndDate = DateTime.UtcNow;
                    }

                    _context.StudentAssignments.UpdateRange(existing);
                }

                // MEJORADO: Obtener año académico activo una vez para todas las asignaciones
                var student = await _context.Users.FindAsync(studentId);
                var schoolId = student?.SchoolId;
                var activeAcademicYear = schoolId.HasValue 
                    ? await _academicYearService.GetActiveAcademicYearAsync(schoolId.Value)
                    : null;

                foreach (var item in assignments)
                {
                    Console.WriteLine($"[StudentAssignmentService] Agregando asignación: GradeId={item.GradeId}, GroupId={item.GroupId}");
                    
                    if (!replaceExistingActive)
                    {
                        var dup = await _context.StudentAssignments.AnyAsync(sa =>
                            sa.StudentId == studentId &&
                            sa.GradeId == item.GradeId &&
                            sa.GroupId == item.GroupId &&
                            sa.IsActive);
                        if (dup)
                            continue;
                    }

                    _context.StudentAssignments.Add(new StudentAssignment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        GradeId = item.GradeId,
                        GroupId = item.GroupId,
                        IsActive = true, // Nueva asignación activa
                        AcademicYearId = activeAcademicYear?.Id, // Asignar año académico si existe
                        CreatedAt = DateTime.UtcNow,
                        EnrollmentType = "Regular",
                        StartDate = DateTime.UtcNow
                    });
                }

                Console.WriteLine($"[StudentAssignmentService] Guardando cambios...");
                await _context.SaveChangesAsync();
                var createdAssignments = await _context.StudentAssignments
                    .Where(sa => sa.StudentId == studentId && sa.IsActive)
                    .OrderByDescending(sa => sa.CreatedAt)
                    .ToListAsync();
                foreach (var assignment in createdAssignments)
                    await SyncStudentSubjectAssignmentsAsync(assignment);
                await _context.SaveChangesAsync();
                Console.WriteLine($"[StudentAssignmentService] AssignAsync completado exitosamente");
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"[StudentAssignmentService] DbUpdateException en AssignAsync: {dbEx.Message}");
                Console.WriteLine($"[StudentAssignmentService] InnerException: {dbEx.InnerException?.Message}");
                throw new InvalidOperationException($"Error al asignar estudiantes. Detalles: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentAssignmentService] Exception general en AssignAsync: {ex.Message}");
                Console.WriteLine($"[StudentAssignmentService] StackTrace: {ex.StackTrace}");
                throw new Exception($"Error inesperado al asignar estudiantes. Detalles: {ex.Message}", ex);
            }
        }

        public async Task RemoveAssignmentsAsync(Guid studentId, Guid? onlyAssignmentId = null)
        {
            // MEJORADO: Inactivar en lugar de eliminar para preservar historial
            var query = _context.StudentAssignments.Where(a => a.StudentId == studentId && a.IsActive);
            if (onlyAssignmentId.HasValue)
                query = query.Where(a => a.Id == onlyAssignmentId.Value);

            var activeAssignments = await query.ToListAsync();

            if (activeAssignments.Any())
            {
                foreach (var assignment in activeAssignments)
                {
                    assignment.IsActive = false;
                    assignment.EndDate = DateTime.UtcNow;
                }

                _context.StudentAssignments.UpdateRange(activeAssignments);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Elimina permanentemente las asignaciones (usar solo cuando sea necesario limpiar datos)
        /// </summary>
        [Obsolete("Usar RemoveAssignmentsAsync que preserva historial. Este método elimina datos permanentemente.")]
        public async Task DeleteAssignmentsPermanentlyAsync(Guid studentId)
        {
            var assignments = await _context.StudentAssignments
                .Where(a => a.StudentId == studentId)
                .ToListAsync();

            _context.StudentAssignments.RemoveRange(assignments);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AssignStudentAsync(Guid studentId, Guid subjectId, Guid gradeId, Guid groupId)
        {
            try
            {
                Console.WriteLine($"[StudentAssignmentService] AssignStudentAsync - StudentId: {studentId}, GradeId: {gradeId}, GroupId: {groupId}");
                
                // Verificar solo asignaciones activas
                bool exists = await _context.StudentAssignments.AnyAsync(sa =>
                    sa.StudentId == studentId &&
                    sa.GradeId == gradeId &&
                    sa.GroupId == groupId &&
                    sa.IsActive
                );

                if (exists)
                {
                    Console.WriteLine($"[StudentAssignmentService] La asignación ya existe");
                    return false;
                }

                // MEJORADO: Obtener año académico activo para la nueva asignación
                var student = await _context.Users.FindAsync(studentId);
                var schoolId = student?.SchoolId;
                var activeAcademicYear = schoolId.HasValue 
                    ? await _academicYearService.GetActiveAcademicYearAsync(schoolId.Value)
                    : null;

                var assignment = new StudentAssignment
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    GradeId = gradeId,
                    GroupId = groupId,
                    IsActive = true,
                    AcademicYearId = activeAcademicYear?.Id, // Asignar año académico si existe
                    CreatedAt = DateTime.UtcNow,
                    EnrollmentType = "Regular",
                    StartDate = DateTime.UtcNow
                };

                Console.WriteLine($"[StudentAssignmentService] Nueva asignación creada con CreatedAt: {assignment.CreatedAt}");
                
                _context.StudentAssignments.Add(assignment);
                await _context.SaveChangesAsync();
                await SyncStudentSubjectAssignmentsAsync(assignment, subjectId);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"[StudentAssignmentService] Asignación guardada exitosamente");
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"[StudentAssignmentService] DbUpdateException en AssignStudentAsync: {dbEx.Message}");
                Console.WriteLine($"[StudentAssignmentService] Inner Exception: {dbEx.InnerException?.Message}");
                throw new InvalidOperationException($"Error al asignar estudiante. Detalles: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentAssignmentService] Exception general en AssignStudentAsync: {ex.Message}");
                Console.WriteLine($"[StudentAssignmentService] Stack Trace: {ex.StackTrace}");
                throw new Exception($"Error inesperado al asignar estudiante. Detalles: {ex.Message}", ex);
            }
        }

        public async Task BulkAssignFromFileAsync(List<(string StudentEmail, string SubjectCode, string GradeName, string GroupName)> rows)
        {
            foreach (var row in rows)
            {
                var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == row.StudentEmail);
                var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Code == row.SubjectCode);
                var grade = await _context.GradeLevels.FirstOrDefaultAsync(g => g.Name == row.GradeName);
                var group = await _context.Groups.FirstOrDefaultAsync(g => g.Name == row.GroupName && g.Grade == row.GradeName);

                if (student == null || subject == null || grade == null || group == null)
                {
                    // puedes loggear error con detalles aquí
                    continue;
                }

                bool alreadyExists = await _context.StudentAssignments.AnyAsync(sa =>
                    sa.StudentId == student.Id &&
                    sa.GradeId == grade.Id &&
                    sa.GroupId == group.Id &&
                    sa.IsActive);

                if (!alreadyExists)
                {
                    // MEJORADO: Obtener año académico activo
                    var activeAcademicYear = student.SchoolId.HasValue
                        ? await _academicYearService.GetActiveAcademicYearAsync(student.SchoolId.Value)
                        : null;

                    _context.StudentAssignments.Add(new StudentAssignment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student.Id,
                        GradeId = grade.Id,
                        GroupId = group.Id,
                        IsActive = true,
                        AcademicYearId = activeAcademicYear?.Id, // Asignar año académico si existe
                        CreatedAt = DateTime.UtcNow,
                        EnrollmentType = "Regular",
                        StartDate = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            var affectedAssignments = await _context.StudentAssignments
                .Where(sa => rows.Select(r => r.StudentEmail).Contains(sa.Student.Email) && sa.IsActive)
                .Include(sa => sa.Student)
                .ToListAsync();
            foreach (var assignment in affectedAssignments)
                await SyncStudentSubjectAssignmentsAsync(assignment);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AddEnrollmentAsync(Guid studentId, Guid gradeId, Guid groupId, string enrollmentType = "Nocturno")
        {
            if (string.IsNullOrWhiteSpace(enrollmentType))
                enrollmentType = "Nocturno";

            var student = await _context.Users.FindAsync(studentId);
            var schoolId = student?.SchoolId;
            var activeAcademicYear = schoolId.HasValue
                ? await _academicYearService.GetActiveAcademicYearAsync(schoolId.Value)
                : null;

            var group = await _context.Groups.FindAsync(groupId);
            var shiftId = group?.ShiftId;

            var exists = await ExistsWithShiftAsync(studentId, gradeId, groupId, shiftId);
            if (exists)
                return false;

            _context.StudentAssignments.Add(new StudentAssignment
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                GradeId = gradeId,
                GroupId = groupId,
                ShiftId = group?.ShiftId,
                IsActive = true,
                AcademicYearId = activeAcademicYear?.Id,
                CreatedAt = DateTime.UtcNow,
                EnrollmentType = enrollmentType.Trim(),
                StartDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            var assignment = await _context.StudentAssignments
                .Where(sa => sa.StudentId == studentId && sa.GradeId == gradeId && sa.GroupId == groupId && sa.IsActive)
                .OrderByDescending(sa => sa.CreatedAt)
                .FirstAsync();
            await SyncStudentSubjectAssignmentsAsync(assignment);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}
