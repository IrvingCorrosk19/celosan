namespace SchoolManager.Helpers;

/// <summary>Número de credencial institucional (separado del formato SM- de estudiantes).</summary>
public static class InstitutionalCardNumberHelper
{
    public static string Generate(Guid userId) =>
        $"IC-{DateTime.UtcNow:yyyyMMdd}-{userId.ToString("N")[..8].ToUpper()}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
}
