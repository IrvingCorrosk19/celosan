using SchoolManager.Dtos;

namespace SchoolManager.Services.Interfaces;

/// <summary>Servicio del módulo Club de Padres: listado de estudiantes, estado de carnet/plataforma, marcar pagado y activar plataforma.</summary>
public interface IClubParentsPaymentService
{
    /// <summary>Lista estudiantes (User student/estudiante) de la escuela del usuario actual, con filtros opcionales. Incluye estado carnet y plataforma.</summary>
    Task<IReadOnlyList<ClubParentsStudentDto>> GetStudentsAsync(
        Guid? gradeId = null,
        Guid? groupId = null,
        string? search = null,
        string? cedula = null);

    /// <summary>Estado de carnet y plataforma de un estudiante. Si no hay registro, devuelve Pendiente/Pendiente.</summary>
    Task<StudentPaymentStatusDto> GetStudentPaymentStatusAsync(Guid studentId);

    /// <summary>Transición Pendiente → Pagado. Crea registro en student_payment_access si no existe.</summary>
    Task MarkCarnetAsPaidAsync(Guid studentId);

    /// <summary>Transición Pendiente → Activo en plataforma. Crea registro si no existe.</summary>
    Task ActivatePlatformAsync(Guid studentId);
}
