using SchoolManager.Dtos;
using SchoolManager.Models;

namespace SchoolManager.Services.Interfaces
{
    public interface IStudentReportService
    {
        Task<StudentReportDto> GetReportByStudentIdAsync(Guid studentId);
        /// <summary>
        /// Devuelve el reporte del estudiante filtrado por trimestre, o <c>null</c> si no hay calificaciones para ese trimestre.
        /// Los callers deben manejar el caso null (sin datos para el trimestre).
        /// </summary>
        Task<StudentReportDto?> GetReportByStudentIdAndTrimesterAsync(Guid studentId, string trimester);
        Task<List<DisciplineReportDto>> GetDisciplineReportsByStudentIdAsync(Guid studentId);
    }
}
