using System;
using System.Collections.Generic;
using SchoolManager.Helpers;

namespace SchoolManager.Services.Interfaces;

public interface IEvaluationSchemeResolver
{
    /// <summary>
    /// Esquema declarado (año + trimestre). Define qué tipos puede crear el docente.
    /// </summary>
    EvaluationScheme Resolve(Guid? schoolId, string? academicYearName, string? trimesterCode);

    /// <summary>
    /// Esquema de cálculo. En 3T 2026 usa Legacy si aún no hay activities del esquema nuevo.
    /// Desde 2027 siempre Weighted, aunque solo existan tipos legacy.
    /// </summary>
    EvaluationScheme ResolveEffective(
        Guid? schoolId,
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?> activityTypes);
}
