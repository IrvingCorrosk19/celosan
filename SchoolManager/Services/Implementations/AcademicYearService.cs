using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using Npgsql;

namespace SchoolManager.Services.Implementations;

public class AcademicYearService : IAcademicYearService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AcademicYearService> _logger;

    public AcademicYearService(SchoolDbContext context, ICurrentUserService currentUserService, ILogger<AcademicYearService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<AcademicYear?> GetActiveAcademicYearAsync(Guid? schoolId = null)
    {
        try
        {
            Guid targetSchoolId;

            if (schoolId.HasValue)
            {
                targetSchoolId = schoolId.Value;
            }
            else
            {
                var currentUserSchool = await _currentUserService.GetCurrentUserSchoolAsync();
                if (currentUserSchool == null)
                    return null;
                
                targetSchoolId = currentUserSchool.Id;
            }

            var now = DateTime.UtcNow;
            var matches = await _context.AcademicYears
                .Where(ay => ay.SchoolId == targetSchoolId
                    && ay.IsActive
                    && ay.StartDate <= now
                    && ay.EndDate >= now)
                .ToListAsync();

            if (matches.Count == 0)
                return null;

            if (matches.Count == 1)
                return matches[0];

            var ids = string.Join(", ", matches.Select(ay => ay.Id));
            _logger.LogError(
                "Inconsistencia de academic_year: {Count} años activos vigentes para SchoolId {SchoolId}. Ids: {Ids}. No se elige uno arbitrariamente.",
                matches.Count, targetSchoolId, ids);
            throw new InvalidOperationException(
                $"Hay {matches.Count} años académicos activos vigentes para la escuela. Debe existir exactamente uno.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01") // Table does not exist
        {
            // La tabla academic_years aún no existe, retornar null (compatibilidad hacia atrás)
            return null;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "42P01")
        {
            // La tabla academic_years aún no existe, retornar null (compatibilidad hacia atrás)
            return null;
        }
    }

    public async Task<AcademicYear?> GetAcademicYearByIdAsync(Guid id)
    {
        try
        {
            return await _context.AcademicYears
                .Include(ay => ay.School)
                .FirstOrDefaultAsync(ay => ay.Id == id);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return null;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "42P01")
        {
            return null;
        }
    }

    public async Task<List<AcademicYear>> GetAllBySchoolAsync(Guid schoolId)
    {
        try
        {
            return await _context.AcademicYears
                .Where(ay => ay.SchoolId == schoolId)
                .OrderByDescending(ay => ay.StartDate)
                .ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return new List<AcademicYear>();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "42P01")
        {
            return new List<AcademicYear>();
        }
    }

    public async Task<AcademicYear> CreateAsync(AcademicYear academicYear)
    {
        academicYear.Id = Guid.NewGuid();
        academicYear.CreatedAt = DateTime.UtcNow;

        _context.AcademicYears.Add(academicYear);
        await _context.SaveChangesAsync();

        return academicYear;
    }

    public async Task<AcademicYear> UpdateAsync(AcademicYear academicYear)
    {
        academicYear.UpdatedAt = DateTime.UtcNow;

        _context.AcademicYears.Update(academicYear);
        await _context.SaveChangesAsync();

        return academicYear;
    }

    /// <summary>
    /// Existe un año con ese nombre en la escuela, ignorando el QueryFilter de tenant.
    /// Solo para Ensure de arranque/alta de escuela, que ya recibe schoolId explícito.
    /// </summary>
    private async Task<bool> ExistsForSchoolByNameAsync(Guid schoolId, string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return false;

        return await _context.AcademicYears
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(ay => ay.SchoolId == schoolId && ay.Name == normalized);
    }

    public async Task EnsureDefaultAcademicYearForSchoolAsync(Guid schoolId)
    {
        try
        {
            var yearName = DateTime.UtcNow.Year.ToString();
            if (await ExistsForSchoolByNameAsync(schoolId, yearName))
                return;

            var year = DateTime.UtcNow.Year;
            var defaultYear = new AcademicYear
            {
                SchoolId = schoolId,
                Name = yearName,
                StartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                IsActive = true
            };
            await CreateAsync(defaultYear);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Carrera con unique (school_id, name): el año ya existe.
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Tabla academic_years no existe; no romper flujo de creación de escuela
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // Unique (school_id, name) ya cubre el año.
        }
        catch (Exception)
        {
            throw; // Re-lanzar para que el llamador decida (transacción puede hacer rollback)
        }
    }
}

