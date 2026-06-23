using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class ScheduleConfigurationService : IScheduleConfigurationService
{
    private readonly SchoolDbContext _context;
    private readonly IShiftService _shiftService;

    public ScheduleConfigurationService(SchoolDbContext context, IShiftService shiftService)
    {
        _context = context;
        _shiftService = shiftService;
    }

    public async Task<SchoolScheduleConfiguration?> GetBySchoolIdAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        return await _context.SchoolScheduleConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SchoolId == schoolId, cancellationToken);
    }

    /// <summary>Fin de la última clase de mañana (sin el hueco previo a tarde).</summary>
    private static TimeOnly ComputeLastClassEndMorning(SchoolScheduleConfiguration model)
    {
        var d = model.MorningBlockDurationMinutes;
        var m = model.MorningBlockCount;
        var k = model.RecessAfterMorningBlockNumber;
        var t = model.MorningStartTime;
        if (k < m)
            return t.AddMinutes(k * d + model.RecessDurationMinutes + (m - k) * d);
        return t.AddMinutes(m * d);
    }

    public async Task<(bool Success, string Message)> SaveAndGenerateBlocksAsync(SchoolScheduleConfiguration model, Guid schoolId, bool forceRegenerate = false, CancellationToken cancellationToken = default)
    {
        var nightStart = model.NightStartTime ?? model.MorningStartTime;
        var nightCount = model.NightBlockCount ?? model.MorningBlockCount;
        var nightDuration = model.NightBlockDurationMinutes ?? model.MorningBlockDurationMinutes;

        if (nightCount < 1 || nightDuration < 1)
            return (false, "La jornada nocturna debe tener al menos 1 bloque y duración positiva.");

        if (model.RecessDurationMinutes < 1 || model.RecessDurationMinutes > 180)
            return (false, "La duración del recreo debe estar entre 1 y 180 minutos.");

        var k = model.RecessAfterMorningBlockNumber;
        if (k < 1 || k > nightCount)
        {
            return (false,
                $"Indique después de qué bloque va el recreo nocturno (1 a {nightCount}). Valor actual: {k}.");
        }

        model.NightStartTime = nightStart;
        model.NightBlockCount = nightCount;
        model.NightBlockDurationMinutes = nightDuration;
        model.MorningStartTime = nightStart;
        model.MorningBlockCount = nightCount;
        model.MorningBlockDurationMinutes = nightDuration;
        model.AfternoonStartTime = null;
        model.AfternoonBlockCount = null;
        model.AfternoonBlockDurationMinutes = null;
        model.RecessAfterAfternoonBlockNumber = model.RecessAfterMorningBlockNumber;

        var slotIds = await _context.TimeSlots
            .Where(t => t.SchoolId == schoolId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (slotIds.Count > 0)
        {
            var hasEntries = await _context.ScheduleEntries
                .AnyAsync(e => slotIds.Contains(e.TimeSlotId), cancellationToken);
            if (hasEntries && !forceRegenerate)
                return (false, "No se puede regenerar la jornada porque ya existen horarios asignados a bloques. Marque «Forzar regeneración» si desea eliminarlos y regenerar (los docentes tendrán que volver a asignar).");
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (slotIds.Count > 0)
            {
                var entriesToRemove = await _context.ScheduleEntries
                    .Where(e => slotIds.Contains(e.TimeSlotId))
                    .ToListAsync(cancellationToken);
                if (entriesToRemove.Count > 0)
                {
                    _context.ScheduleEntries.RemoveRange(entriesToRemove);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            var existing = await _context.SchoolScheduleConfigurations
                .FirstOrDefaultAsync(c => c.SchoolId == schoolId, cancellationToken);

            var now = DateTime.UtcNow;
            if (existing != null)
            {
                existing.MorningStartTime = model.MorningStartTime;
                existing.MorningBlockDurationMinutes = model.MorningBlockDurationMinutes;
                existing.MorningBlockCount = model.MorningBlockCount;
                existing.RecessDurationMinutes = model.RecessDurationMinutes;
                existing.RecessAfterMorningBlockNumber = model.RecessAfterMorningBlockNumber;
                existing.RecessAfterAfternoonBlockNumber = model.RecessAfterAfternoonBlockNumber;
                existing.AfternoonStartTime = model.AfternoonStartTime;
                existing.AfternoonBlockDurationMinutes = model.AfternoonBlockDurationMinutes;
                existing.AfternoonBlockCount = model.AfternoonBlockCount;
                existing.NightStartTime = model.NightStartTime;
                existing.NightBlockDurationMinutes = model.NightBlockDurationMinutes;
                existing.NightBlockCount = model.NightBlockCount;
                existing.UpdatedAt = now;
            }
            else
            {
                _context.SchoolScheduleConfigurations.Add(new SchoolScheduleConfiguration
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    MorningStartTime = model.MorningStartTime,
                    MorningBlockDurationMinutes = model.MorningBlockDurationMinutes,
                    MorningBlockCount = model.MorningBlockCount,
                    RecessDurationMinutes = model.RecessDurationMinutes,
                    RecessAfterMorningBlockNumber = model.RecessAfterMorningBlockNumber,
                    RecessAfterAfternoonBlockNumber = model.RecessAfterAfternoonBlockNumber,
                    AfternoonStartTime = model.AfternoonStartTime,
                    AfternoonBlockDurationMinutes = model.AfternoonBlockDurationMinutes,
                    AfternoonBlockCount = model.AfternoonBlockCount,
                    NightStartTime = model.NightStartTime,
                    NightBlockDurationMinutes = model.NightBlockDurationMinutes,
                    NightBlockCount = model.NightBlockCount,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            var toRemove = await _context.TimeSlots
                .Where(t => t.SchoolId == schoolId)
                .ToListAsync(cancellationToken);
            _context.TimeSlots.RemoveRange(toRemove);
            await _context.SaveChangesAsync(cancellationToken);

            var shiftNoche = await _shiftService.GetOrCreateBySchoolAndNameAsync(schoolId, "Noche");
            var displayOrder = 0;
            var start = model.NightStartTime!.Value;
            var m = model.NightBlockCount!.Value;
            var d = model.NightBlockDurationMinutes!.Value;
            var recMin = model.RecessDurationMinutes;

            void AddClassBlock(int blockIndex1Based)
            {
                var end = start.AddMinutes(d);
                _context.TimeSlots.Add(new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ShiftId = shiftNoche.Id,
                    Name = $"Bloque {blockIndex1Based}",
                    StartTime = start,
                    EndTime = end,
                    DisplayOrder = displayOrder++,
                    IsActive = true,
                    CreatedAt = now
                });
                start = end;
            }

            if (k < m)
            {
                for (var i = 1; i <= k; i++)
                    AddClassBlock(i);
                var recessEnd = start.AddMinutes(recMin);
                _context.TimeSlots.Add(new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ShiftId = shiftNoche.Id,
                    Name = "Recreo",
                    StartTime = start,
                    EndTime = recessEnd,
                    DisplayOrder = displayOrder++,
                    IsActive = true,
                    CreatedAt = now
                });
                start = recessEnd;
                for (var i = k + 1; i <= m; i++)
                    AddClassBlock(i);
            }
            else
            {
                for (var i = 1; i <= m; i++)
                    AddClassBlock(i);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (true, "Configuración nocturna guardada y bloques de Noche generados correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, "Error al guardar: " + ex.Message);
        }
    }
}
