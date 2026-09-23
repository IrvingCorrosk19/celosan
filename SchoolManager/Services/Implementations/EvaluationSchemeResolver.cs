using System;
using System.Collections.Generic;
using System.Linq;
using SchoolManager.Helpers;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

/// <summary>
/// Único punto que decide Legacy vs Weighted801010.
/// 2026: 1T/2T Legacy, 3T Weighted. 2027+: todos Weighted.
/// Sin persistencia; se puede sustituir después por evaluation_schemes.
/// </summary>
public class EvaluationSchemeResolver : IEvaluationSchemeResolver
{
    public EvaluationScheme Resolve(Guid? schoolId, string? academicYearName, string? trimesterCode) =>
        ResolveCore(schoolId, academicYearName, trimesterCode);

    public EvaluationScheme ResolveEffective(
        Guid? schoolId,
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?> activityTypes) =>
        ResolveEffectiveCore(schoolId, academicYearName, trimesterCode, activityTypes);

    public static EvaluationScheme Resolve(string? academicYearName, string? trimesterCode) =>
        ResolveCore(null, academicYearName, trimesterCode);

    public static EvaluationScheme ResolveEffective(
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?>? activityTypes) =>
        ResolveEffectiveCore(null, academicYearName, trimesterCode, activityTypes);

    /// <summary>
    /// 2026 3T: Weighted solo si ya existe Unidireccional/Autoevaluación/Coevaluación.
    /// Si no, Legacy transitorio. 2027+: Weighted siempre.
    /// </summary>
    public static EvaluationScheme ResolveEffectiveCore(
        Guid? schoolId,
        string? academicYearName,
        string? trimesterCode,
        IEnumerable<string?>? activityTypes)
    {
        var declared = ResolveCore(schoolId, academicYearName, trimesterCode);
        if (declared != EvaluationScheme.Weighted801010)
            return declared;

        if (TryParseAcademicYear(academicYearName, out var year) && year >= 2027)
            return EvaluationScheme.Weighted801010;

        if (HasWeightedCategory(activityTypes))
            return EvaluationScheme.Weighted801010;

        return EvaluationScheme.Legacy;
    }

    public static bool HasWeightedCategory(IEnumerable<string?>? activityTypes) =>
        activityTypes != null && activityTypes.Any(EvaluationActivityTypes.IsWeightedType);

    public static EvaluationScheme ResolveCore(Guid? schoolId, string? academicYearName, string? trimesterCode)
    {
        _ = schoolId;

        if (!TryParseAcademicYear(academicYearName, out var year))
            return EvaluationScheme.Legacy;

        if (year >= 2027)
            return EvaluationScheme.Weighted801010;

        if (year == 2026)
        {
            var trimester = OfficialGradeService.NormalizeTrimester(trimesterCode);
            return trimester == "3T"
                ? EvaluationScheme.Weighted801010
                : EvaluationScheme.Legacy;
        }

        return EvaluationScheme.Legacy;
    }

    /// <summary>
    /// Acepta "2026" o prefijo de 4 dígitos ("2026-2027").
    /// No usa DateTime.Now.
    /// </summary>
    public static bool TryParseAcademicYear(string? name, out int year)
    {
        year = 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        if (int.TryParse(trimmed, out year) && IsPlausibleYear(year))
            return true;

        for (var i = 0; i <= trimmed.Length - 4; i++)
        {
            if (char.IsDigit(trimmed[i]) &&
                char.IsDigit(trimmed[i + 1]) &&
                char.IsDigit(trimmed[i + 2]) &&
                char.IsDigit(trimmed[i + 3]) &&
                int.TryParse(trimmed.AsSpan(i, 4), out year) &&
                IsPlausibleYear(year))
            {
                return true;
            }
        }

        year = 0;
        return false;
    }

    private static bool IsPlausibleYear(int year) => year >= 1900 && year <= 3000;
}
