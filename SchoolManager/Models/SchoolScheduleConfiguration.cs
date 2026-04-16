using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SchoolManager.Models;

/// <summary>
/// Configuración de jornada escolar por escuela. Una sola por escuela.
/// Define horario de mañana (y opcionalmente tarde) para generar bloques (TimeSlots) automáticamente.
/// Horas en formato 24 h (07:00, 13:00) estándar en la aplicación.
/// </summary>
public class SchoolScheduleConfiguration
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    /// <summary>Hora de inicio de la jornada de mañana (formato 24 h, ej. 07:00).</summary>
    [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
    public TimeOnly MorningStartTime { get; set; }
    public int MorningBlockDurationMinutes { get; set; }
    public int MorningBlockCount { get; set; }

    /// <summary>
    /// Duración del recreo entre clases (minutos): misma duración para recreos en medio de mañana y de tarde;
    /// además es el mínimo del hueco entre última clase de mañana e inicio de tarde.
    /// </summary>
    [Range(1, 180)]
    public int RecessDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Después de qué bloque de clase de mañana (numeración 1..N) se coloca el recreo.
    /// Si es igual a la cantidad de bloques de mañana, no hay recreo entre clases: el recreo es solo el intervalo hasta la hora de inicio de tarde (requiere jornada tarde).
    /// </summary>
    [Range(1, 40)]
    public int RecessAfterMorningBlockNumber { get; set; } = 4;

    /// <summary>
    /// Copia del índice de recreo de mañana (misma posición en tarde); se asigna al guardar. Solo persistencia/compatibilidad.
    /// </summary>
    [Range(1, 40)]
    public int RecessAfterAfternoonBlockNumber { get; set; } = 4;

    /// <summary>Hora de inicio de la jornada de tarde (formato 24 h, ej. 13:00); null si solo hay mañana.</summary>
    [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
    public TimeOnly? AfternoonStartTime { get; set; }
    public int? AfternoonBlockDurationMinutes { get; set; }
    public int? AfternoonBlockCount { get; set; }

    /// <summary>Hora de inicio de la jornada nocturna (formato 24 h, ej. 18:00); null si no aplica.</summary>
    [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
    public TimeOnly? NightStartTime { get; set; }
    public int? NightBlockDurationMinutes { get; set; }
    public int? NightBlockCount { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [ValidateNever]
    public virtual School School { get; set; } = null!;
}
