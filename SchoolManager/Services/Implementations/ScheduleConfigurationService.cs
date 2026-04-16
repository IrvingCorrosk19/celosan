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
        if (model.MorningBlockCount < 1 || model.MorningBlockDurationMinutes < 1)
            return (false, "La jornada de mañana debe tener al menos 1 bloque y duración positiva.");

        if (model.RecessDurationMinutes < 1 || model.RecessDurationMinutes > 180)
            return (false, "La duración del recreo debe estar entre 1 y 180 minutos.");

        var k = model.RecessAfterMorningBlockNumber;
        if (k < 1 || k > model.MorningBlockCount)
        {
            return (false,
                $"Indique después de qué bloque va el recreo (1 a {model.MorningBlockCount}). Valor actual: {k}.");
        }

        var afternoonActive = model.AfternoonStartTime.HasValue
            && (model.AfternoonBlockCount ?? 0) > 0
            && (model.AfternoonBlockDurationMinutes ?? 0) > 0;
        var nightActive = model.NightStartTime.HasValue
            && (model.NightBlockCount ?? 0) > 0
            && (model.NightBlockDurationMinutes ?? 0) > 0;

        if (!afternoonActive)
        {
            if (model.MorningBlockCount < 2)
                return (false,
                    "Con jornada solo mañana hacen falta al menos 2 bloques para colocar un recreo entre clases, o bien configure jornada de tarde.");
            if (k >= model.MorningBlockCount)
                return (false,
                    "Con jornada solo mañana el recreo debe ir después de un bloque anterior al último (elija un número menor que la cantidad de bloques de mañana).");
        }

        // Tarde: si pone hora de inicio, duración y cantidad deben ser ambas positivas
        var hasAfternoonStart = model.AfternoonStartTime.HasValue;
        var afternoonCount = model.AfternoonBlockCount ?? 0;
        var afternoonDuration = model.AfternoonBlockDurationMinutes ?? 0;
        if (hasAfternoonStart && (afternoonCount > 0 || afternoonDuration > 0))
        {
            if (afternoonCount < 1 || afternoonDuration < 1)
                return (false, "Si configura jornada tarde, complete duración (min) y cantidad de bloques con valores mayores a 0.");
        }

        // Noche: si pone hora de inicio, duración y cantidad deben ser ambas positivas
        var hasNightStart = model.NightStartTime.HasValue;
        var nightCount = model.NightBlockCount ?? 0;
        var nightDuration = model.NightBlockDurationMinutes ?? 0;
        if (hasNightStart && (nightCount > 0 || nightDuration > 0))
        {
            if (nightCount < 1 || nightDuration < 1)
                return (false, "Si configura jornada nocturna, complete duración (min) y cantidad de bloques con valores mayores a 0.");
        }

        // Tarde usa el mismo "después del bloque n.º" que mañana (misma duración de recreo), acotado a la cantidad de bloques de tarde.
        if (afternoonActive)
            model.RecessAfterAfternoonBlockNumber = model.RecessAfterMorningBlockNumber;

        var lastMorningClassEnd = ComputeLastClassEndMorning(model);
        if (afternoonActive)
        {
            var afternoonStart = model.AfternoonStartTime!.Value;
            if (afternoonStart < lastMorningClassEnd)
                return (false, "La jornada de tarde debe comenzar después del último bloque de mañana (sin solapamientos).");

            var gapMinutes = (int)(afternoonStart - lastMorningClassEnd).TotalMinutes;
            if (gapMinutes < model.RecessDurationMinutes)
            {
                return (false,
                    $"Entre el fin de la última clase de mañana ({lastMorningClassEnd:HH:mm}) y el inicio de tarde debe haber al menos {model.RecessDurationMinutes} min de recreo (ahora hay {gapMinutes} min). Ajuste la hora de inicio de tarde o la configuración de bloques.");
            }
        }

        if (nightActive)
        {
            var nightStart = model.NightStartTime!.Value;
            var comparisonStart = afternoonActive
                ? model.AfternoonStartTime!.Value.AddMinutes((model.AfternoonBlockCount!.Value * model.AfternoonBlockDurationMinutes!.Value) + model.RecessDurationMinutes)
                : lastMorningClassEnd;
            if (nightStart <= comparisonStart)
                return (false, "La jornada nocturna debe iniciar después de la jornada previa (mañana/tarde) para evitar solapamientos.");
        }

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

            var shiftManana = await _shiftService.GetOrCreateBySchoolAndNameAsync(schoolId, "Mañana");
            Shift? shiftTarde = null;
            if (afternoonActive)
                shiftTarde = await _shiftService.GetOrCreateBySchoolAndNameAsync(schoolId, "Tarde");
            Shift? shiftNoche = null;
            if (nightActive)
                shiftNoche = await _shiftService.GetOrCreateBySchoolAndNameAsync(schoolId, "Noche");

            var displayOrder = 0;
            var start = model.MorningStartTime;
            var m = model.MorningBlockCount;
            var d = model.MorningBlockDurationMinutes;
            var recMin = model.RecessDurationMinutes;

            void AddClassBlock(int blockIndex1Based)
            {
                var end = start.AddMinutes(d);
                _context.TimeSlots.Add(new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ShiftId = shiftManana.Id,
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
                    ShiftId = shiftManana.Id,
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

            if (afternoonActive && model.AfternoonStartTime.HasValue)
            {
                var aft = model.AfternoonStartTime.Value;
                if (aft > start)
                {
                    _context.TimeSlots.Add(new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = schoolId,
                        ShiftId = shiftManana.Id,
                        Name = "Recreo",
                        StartTime = start,
                        EndTime = aft,
                        DisplayOrder = displayOrder++,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
                start = aft;
            }

            if (shiftTarde != null && model.AfternoonStartTime.HasValue && (model.AfternoonBlockCount ?? 0) > 0 && (model.AfternoonBlockDurationMinutes ?? 0) > 0)
            {
                var count = model.AfternoonBlockCount!.Value;
                var duration = model.AfternoonBlockDurationMinutes!.Value;
                var kAfternoon = Math.Clamp(model.RecessAfterMorningBlockNumber, 1, count);
                start = model.AfternoonStartTime!.Value;

                void AddAfternoonClassBlock(int blockIndex1Based)
                {
                    var end = start.AddMinutes(duration);
                    _context.TimeSlots.Add(new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = schoolId,
                        ShiftId = shiftTarde.Id,
                        Name = $"Bloque {blockIndex1Based}",
                        StartTime = start,
                        EndTime = end,
                        DisplayOrder = displayOrder++,
                        IsActive = true,
                        CreatedAt = now
                    });
                    start = end;
                }

                if (kAfternoon < count)
                {
                    for (var i = 1; i <= kAfternoon; i++)
                        AddAfternoonClassBlock(i);
                    var recessEndAfternoon = start.AddMinutes(recMin);
                    _context.TimeSlots.Add(new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = schoolId,
                        ShiftId = shiftTarde.Id,
                        Name = "Recreo",
                        StartTime = start,
                        EndTime = recessEndAfternoon,
                        DisplayOrder = displayOrder++,
                        IsActive = true,
                        CreatedAt = now
                    });
                    start = recessEndAfternoon;
                    for (var i = kAfternoon + 1; i <= count; i++)
                        AddAfternoonClassBlock(i);
                }
                else
                {
                    for (var i = 1; i <= count; i++)
                        AddAfternoonClassBlock(i);
                }
            }

            if (shiftNoche != null && model.NightStartTime.HasValue && (model.NightBlockCount ?? 0) > 0 && (model.NightBlockDurationMinutes ?? 0) > 0)
            {
                start = model.NightStartTime.Value;
                var count = model.NightBlockCount!.Value;
                var duration = model.NightBlockDurationMinutes!.Value;
                var kNight = Math.Clamp(model.RecessAfterMorningBlockNumber, 1, count);

                void AddNightClassBlock(int blockIndex1Based)
                {
                    var end = start.AddMinutes(duration);
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

                if (kNight < count)
                {
                    for (var i = 1; i <= kNight; i++)
                        AddNightClassBlock(i);

                    var recessEndNight = start.AddMinutes(recMin);
                    _context.TimeSlots.Add(new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = schoolId,
                        ShiftId = shiftNoche.Id,
                        Name = "Recreo",
                        StartTime = start,
                        EndTime = recessEndNight,
                        DisplayOrder = displayOrder++,
                        IsActive = true,
                        CreatedAt = now
                    });
                    start = recessEndNight;
                    for (var i = kNight + 1; i <= count; i++)
                        AddNightClassBlock(i);
                }
                else
                {
                    for (var i = 1; i <= count; i++)
                        AddNightClassBlock(i);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (true, "Configuración guardada y bloques generados correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, "Error al guardar: " + ex.Message);
        }
    }
}
