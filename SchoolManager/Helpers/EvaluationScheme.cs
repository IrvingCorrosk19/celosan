namespace SchoolManager.Helpers;

/// <summary>
/// Esquema de cálculo de nota trimestral a partir de activities.
/// No aplica a notas importadas (ImportedTrimesterGrade tiene prioridad).
/// </summary>
public enum EvaluationScheme
{
    /// <summary>
    /// Media simple de tipos presentes: notas de apreciación, ejercicios diarios, examen final.
    /// </summary>
    Legacy = 0,

    /// <summary>
    /// Unidireccional 80 % + Autoevaluación 10 % + Coevaluación 10 %, reponderando ausentes.
    /// </summary>
    Weighted801010 = 1
}
