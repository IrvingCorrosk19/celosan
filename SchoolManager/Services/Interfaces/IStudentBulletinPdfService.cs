using SchoolManager.Dtos;

namespace SchoolManager.Services.Interfaces;

/// <summary>PDF de solo lectura del boletín. No recalcula notas; pinta los DTOs de IStudentBulletinService.</summary>
public interface IStudentBulletinPdfService
{
    byte[] GenerateByGradePdf(StudentBulletinDto bulletin, string schoolName);

    byte[] GenerateProgramHistoryPdf(StudentProgramHistoryDto history, string schoolName);

    string BuildFileName(string? studentName, string? academicYear, bool isProgramComplete);
}
