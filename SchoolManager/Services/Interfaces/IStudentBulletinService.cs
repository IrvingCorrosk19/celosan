using SchoolManager.Dtos;

namespace SchoolManager.Services.Interfaces;

public interface IStudentBulletinService
{
    /// <summary>Boletín por grado del ciclo actual. Solo lectura. Un único estudiante.</summary>
    Task<StudentBulletinDto?> GetByGradeBulletinAsync(Guid studentId);

    /// <summary>
    /// Historial por programa/grado. Incluye matrículas activas e inactivas.
    /// Solo lectura. Un único estudiante.
    /// </summary>
    Task<StudentProgramHistoryDto?> GetProgramHistoryAsync(Guid studentId);
}
