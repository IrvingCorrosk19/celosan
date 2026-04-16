using SchoolManager.Models;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations
{
public class AttendanceService : IAttendanceService
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AttendanceService(SchoolDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<Attendance>> GetAllAsync() =>
        await _context.Attendances.ToListAsync();

    public async Task<Attendance?> GetByIdAsync(Guid id) =>
        await _context.Attendances.FindAsync(id);

    public async Task CreateAsync(Attendance attendance)
    {
        // Configurar campos de auditoría y SchoolId
        await AuditHelper.SetAuditFieldsForCreateAsync(attendance, _currentUserService);
        await AuditHelper.SetSchoolIdAsync(attendance, _currentUserService);
        
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Attendance attendance)
    {
        // Configurar campos de auditoría para actualización
        await AuditHelper.SetAuditFieldsForUpdateAsync(attendance, _currentUserService);
        
        _context.Attendances.Update(attendance);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var attendance = await _context.Attendances.FindAsync(id);
        if (attendance != null)
        {
            _context.Attendances.Remove(attendance);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Attendance>> GetByStudentAsync(Guid studentId)
    {
        return await _context.Attendances
            .Where(a => a.StudentId == studentId)
            .ToListAsync();
    }

    public async Task<List<Attendance>> GetHistorialAsync(Guid groupId, Guid gradeId, DateOnly? fechaInicio, DateOnly? fechaFin, Guid? studentId = null, Guid? shiftId = null)
    {
        var query = _context.Attendances
            .Where(a => a.GroupId == groupId && a.GradeId == gradeId);

        if (fechaInicio.HasValue && fechaFin.HasValue)
            query = query.Where(a => a.Date >= fechaInicio.Value && a.Date <= fechaFin.Value);
        // Si no hay fechas, no se filtra por fecha

        if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            query = query.Where(a => a.StudentId == studentId);
        }

        if (shiftId.HasValue && shiftId.Value != Guid.Empty)
        {
            query = query.Where(a => a.ShiftId == shiftId);
        }

        return await query
            .Include(a => a.Student)
            .Include(a => a.Group)
            .Include(a => a.Grade)
            .Include(a => a.Shift)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.Student.Name)
            .ToListAsync();
    }

    public async Task<EstadisticasAsistenciaDto> GetEstadisticasAsync(Guid groupId, Guid gradeId, string trimestre, DateTime fechaInicio, DateTime fechaFin, Guid? shiftId = null)
    {
        var fechaInicioOnly = DateOnly.FromDateTime(fechaInicio);
        var fechaFinOnly = DateOnly.FromDateTime(fechaFin);

        var asistencias = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.GroupId == groupId
                && a.GradeId == gradeId
                && a.Status != null
                && a.Date >= fechaInicioOnly
                && a.Date <= fechaFinOnly)
            .ToListAsync();

        if (shiftId.HasValue && shiftId.Value != Guid.Empty)
            asistencias = asistencias.Where(a => a.ShiftId == shiftId).ToList();

        var total = asistencias.Count;
        var totalPresentes = asistencias.Count(a => a.Status == "present");
        var totalAusentes = asistencias.Count(a => a.Status == "absent");
        var totalTardanzas = asistencias.Count(a => a.Status == "late");
        var totalFugas = asistencias.Count(a => a.Status == "fuga");
        var totalExcusas = asistencias.Count(a => a.Status == "excusa");

        decimal porcAsistencia = total > 0 ? Math.Round((decimal)totalPresentes * 100 / total, 1) : 0;
        decimal porcAusencias = total > 0 ? Math.Round((decimal)totalAusentes * 100 / total, 1) : 0;
        decimal porcTardanzas = total > 0 ? Math.Round((decimal)totalTardanzas * 100 / total, 1) : 0;
        decimal porcFugas = total > 0 ? Math.Round((decimal)totalFugas * 100 / total, 1) : 0;
        decimal porcExcusas = total > 0 ? Math.Round((decimal)totalExcusas * 100 / total, 1) : 0;

        var porEstudiante = asistencias
            .GroupBy(a => new { Name = a.Student?.Name, DocumentId = a.Student?.DocumentId })
            .Select(g => new EstadisticaEstudianteDto
            {
                Estudiante = g.Key.Name ?? "-",
                DocumentId = g.Key.DocumentId ?? "",
                Presentes = g.Count(a => a.Status == "present"),
                Ausentes = g.Count(a => a.Status == "absent"),
                Tardanzas = g.Count(a => a.Status == "late"),
                PorcentajeAsistencia = g.Count() > 0 ? Math.Round((decimal)g.Count(a => a.Status == "present") * 100 / g.Count(), 1) : 0
            })
            .OrderBy(e => e.Estudiante)
            .ToList();

        return new EstadisticasAsistenciaDto
        {
            TotalRegistros = total,
            TotalPresentes = totalPresentes,
            TotalAusentes = totalAusentes,
            TotalTardanzas = totalTardanzas,
            TotalFugas = totalFugas,
            TotalExcusas = totalExcusas,
            PorcentajeAsistencia = porcAsistencia,
            PorcentajeAusencias = porcAusencias,
            PorcentajeTardanzas = porcTardanzas,
            PorcentajeFugas = porcFugas,
            PorcentajeExcusas = porcExcusas,
            PorEstudiante = porEstudiante
        };
    }

    public async Task SaveAttendancesAsync(List<AttendanceSaveDto> attendances)
    {
        if (attendances == null || attendances.Count == 0)
            throw new ArgumentException("No se recibieron asistencias.");

        foreach (var dto in attendances)
        {
            StudentAssignment? matchedAssignment = null;
            if (dto.StudentAssignmentId.HasValue && dto.StudentAssignmentId.Value != Guid.Empty)
            {
                matchedAssignment = await _context.StudentAssignments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(sa => sa.Id == dto.StudentAssignmentId.Value && sa.IsActive);
            }

            if (matchedAssignment == null)
            {
                var assignmentsQuery = _context.StudentAssignments
                    .AsNoTracking()
                    .Where(sa =>
                        sa.StudentId == dto.StudentId &&
                        sa.GroupId == dto.GroupId &&
                        sa.GradeId == dto.GradeId &&
                        sa.IsActive);

                if (dto.ShiftId.HasValue && dto.ShiftId.Value != Guid.Empty)
                    assignmentsQuery = assignmentsQuery.Where(sa => sa.ShiftId == dto.ShiftId.Value);

                matchedAssignment = await assignmentsQuery
                    .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            var effectiveShiftId = dto.ShiftId;
            if ((!effectiveShiftId.HasValue || effectiveShiftId == Guid.Empty) && matchedAssignment?.ShiftId != null)
                effectiveShiftId = matchedAssignment.ShiftId;

            var existingAttendance = await _context.Attendances.FirstOrDefaultAsync(a =>
                a.StudentId == dto.StudentId &&
                a.GroupId == dto.GroupId &&
                a.GradeId == dto.GradeId &&
                a.Date == dto.Date &&
                a.ShiftId == effectiveShiftId);

            if (existingAttendance != null)
            {
                existingAttendance.Status = dto.Status;
                existingAttendance.TeacherId = dto.TeacherId;
                existingAttendance.ShiftId = effectiveShiftId;
                existingAttendance.StudentAssignmentId = matchedAssignment?.Id;
                existingAttendance.UpdatedAt = DateTime.UtcNow;
                await AuditHelper.SetAuditFieldsForUpdateAsync(existingAttendance, _currentUserService);
                continue;
            }

            var attendance = new Attendance
            {
                Id = Guid.NewGuid(),
                StudentId = dto.StudentId,
                TeacherId = dto.TeacherId,
                GroupId = dto.GroupId,
                GradeId = dto.GradeId,
                ShiftId = effectiveShiftId,
                StudentAssignmentId = matchedAssignment?.Id,
                Date = dto.Date,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow
            };
            await AuditHelper.SetAuditFieldsForCreateAsync(attendance, _currentUserService);
            await AuditHelper.SetSchoolIdAsync(attendance, _currentUserService);
            _context.Attendances.Add(attendance);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<AttendanceResponseDto>> GetAttendancesByDateAsync(Guid groupId, Guid gradeId, DateOnly date, Guid? shiftId = null)
    {
        var query = _context.Attendances
            .Where(a => a.GroupId == groupId && a.GradeId == gradeId && a.Date == date);

        if (shiftId.HasValue && shiftId.Value != Guid.Empty)
            query = query.Where(a => a.ShiftId == shiftId);

        return await query
            .Include(a => a.Student)
            .Select(a => new AttendanceResponseDto
            {
                StudentId = a.StudentId ?? Guid.Empty,
                StudentName = a.Student != null ? $"{a.Student.Name} {a.Student.LastName}" : "",
                Status = a.Status,
                Date = a.Date
            })
            .ToListAsync();
    }

    public async Task<List<object>> GetHistorialAsistenciaAsync(HistorialAsistenciaFiltroDto filtro)
    {
        if (filtro == null || filtro.GroupId == Guid.Empty || filtro.GradeId == Guid.Empty)
            throw new ArgumentException("Faltan datos para la consulta.");

        var studentId = string.IsNullOrEmpty(filtro.StudentId) ? (Guid?)null : Guid.Parse(filtro.StudentId);
        var lista = await GetHistorialAsync(
            filtro.GroupId,
            filtro.GradeId,
            filtro.FechaInicio,
            filtro.FechaFin,
            studentId,
            filtro.ShiftId
        );

        var resultado = lista.Select(a => new {
            estudiante = a.Student?.Name,
            documentId = a.Student?.DocumentId,
            fecha = a.Date.ToString("yyyy-MM-dd"),
            estado = a.Status,
            grupo = a.Group?.Name,
            grado = a.Grade?.Name
        }).ToList<object>();

        return resultado;
    }
}
}
