using SchoolManager.Dtos;

namespace SchoolManager.Services.Interfaces;

/// <summary>PDF de solo lectura del boletín. No recalcula notas; pinta los DTOs de IStudentBulletinService.</summary>
public interface IStudentBulletinPdfService
{
    Task<BulletinPdfIdentity> BuildIdentityAsync(string? schoolName, string? logoUrl, CancellationToken cancellationToken = default);

    byte[] GenerateByGradePdf(StudentBulletinDto bulletin, BulletinPdfIdentity identity);

    byte[] GenerateProgramHistoryPdf(StudentProgramHistoryDto history, BulletinPdfIdentity identity);

    string BuildFileName(string? studentName, string? academicYear, bool isProgramComplete);
}
