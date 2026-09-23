using System.Collections.Generic;
using SchoolManager.Helpers;

namespace SchoolManager.Dtos;

public class EvaluationContextDto
{
    public string? AcademicYear { get; set; }
    public string Scheme { get; set; } = EvaluationScheme.Legacy.ToString();
    public string CalculationScheme { get; set; } = EvaluationScheme.Legacy.ToString();
    public IReadOnlyList<EvaluationActivityTypeOption> Types { get; set; } = [];
}

public class EvaluationNotesPayloadDto
{
    public EvaluationContextDto Context { get; set; } = new();
    public Dictionary<string, decimal> ImportedByStudent { get; set; } = new();
    public object Notes { get; set; } = new List<object>();
}
