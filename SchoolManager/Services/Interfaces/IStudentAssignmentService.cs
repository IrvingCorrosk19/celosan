using System.Collections.Generic;
using SchoolManager.Models;

public interface IStudentAssignmentService
{
    Task<List<StudentAssignment>> GetAssignmentsByStudentIdAsync(Guid studentId, bool activeOnly = true);

    /// <summary>
    /// Una sola consulta con JOIN por escuela del usuario actual (evita IN masivo de UUIDs).
    /// Mismo criterio que GetAllStudentsAsync: school_id + rol student/estudiante, solo asignaciones activas.
    /// </summary>
    Task<Dictionary<Guid, List<StudentAssignment>>> GetActiveAssignmentsForCurrentSchoolAsync();

    Task AssignAsync(Guid studentId, List<(Guid SubjectId, Guid GradeId, Guid GroupId)> assignments, bool replaceExistingActive = true);

    Task<bool> AssignStudentAsync(Guid studentId, Guid subjectId, Guid gradeId, Guid groupId); // ← NUEVO

    /// <param name="onlyAssignmentId">Si tiene valor, solo se inactiva esa matrícula; si es null, todas las activas del estudiante.</param>
    Task RemoveAssignmentsAsync(Guid studentId, Guid? onlyAssignmentId = null);

    /// <summary>Agrega una matrícula activa sin inactivar las demás (estudiante en varios grupos).</summary>
    Task<bool> AddEnrollmentAsync(Guid studentId, Guid gradeId, Guid groupId, string enrollmentType = "Nocturno");

    Task BulkAssignFromFileAsync(List<(string StudentEmail, string SubjectCode, string GradeName, string GroupName)> rows);
    Task<bool> ExistsAsync(Guid studentId, Guid gradeId, Guid groupId);

    /// <summary>Matrícula activa mismo grado/grupo y misma jornada (null solo coincide con null).</summary>
    Task<bool> ExistsWithShiftAsync(Guid studentId, Guid gradeId, Guid groupId, Guid? shiftId);

    Task InsertAsync(StudentAssignment assignment);


}
