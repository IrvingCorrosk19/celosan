namespace SchoolManager.Helpers;

public static class GradeImportErrorCodes
{
    public const string StudentNotFound = "STUDENT_NOT_FOUND";
    public const string StudentIdEmailMismatch = "STUDENT_ID_EMAIL_MISMATCH";
    public const string AcademicYearInvalid = "ACADEMIC_YEAR_INVALID";
    public const string GradeNotFound = "GRADE_NOT_FOUND";
    public const string GroupNotFound = "GROUP_NOT_FOUND";
    public const string ShiftNotFound = "SHIFT_NOT_FOUND";
    public const string EnrollmentNotFound = "ENROLLMENT_NOT_FOUND";
    public const string EnrollmentAmbiguous = "ENROLLMENT_AMBIGUOUS";
    public const string EnrollmentHistorical = "ENROLLMENT_HISTORICAL";
    public const string SubjectNotFound = "SUBJECT_NOT_FOUND";
    public const string SubjectAmbiguous = "SUBJECT_AMBIGUOUS";
    public const string SsaNotFound = "SSA_NOT_FOUND";
    public const string SsaInactive = "SSA_INACTIVE";
    public const string InvalidTrimester = "INVALID_TRIMESTER";
    public const string InvalidScore = "INVALID_SCORE";
    public const string DuplicateExcelRow = "DUPLICATE_EXCEL_ROW";
    public const string ImportConflict = "IMPORT_CONFLICT";
    public const string UnknownValidationError = "UNKNOWN_VALIDATION_ERROR";
}

public sealed class GradeImportValidationError
{
    public string Code { get; init; } = GradeImportErrorCodes.UnknownValidationError;
    public string Message { get; init; } = string.Empty;
    public string ExcelValue { get; init; } = string.Empty;
    public string Found { get; init; } = string.Empty;
    public string Review { get; init; } = string.Empty;
}

public static class GradeImportErrorFactory
{
    public static GradeImportValidationError StudentNotFound(string document, string email) =>
        Build(
            GradeImportErrorCodes.StudentNotFound,
            "No se encontró un estudiante con ese documento o email en esta escuela.",
            Excel("Documento", document, "Email", email),
            "Ningún estudiante coincide.",
            "Revise documento y email en el Excel.");

    public static GradeImportValidationError StudentIdEmailMismatch(string document, string email, string detail) =>
        Build(
            GradeImportErrorCodes.StudentIdEmailMismatch,
            detail,
            Excel("Documento", document, "Email", email),
            "Documento y email no corresponden a la misma persona.",
            "Verifique que ambas columnas sean del mismo estudiante.");

    public static GradeImportValidationError AcademicYearInvalid(string year) =>
        Build(
            GradeImportErrorCodes.AcademicYearInvalid,
            "El año académico no es válido para esta escuela.",
            Excel("Año", year),
            "Año no autorizado o inexistente.",
            "Seleccione un año académico de la escuela.");

    public static GradeImportValidationError GradeNotFound(string grade) =>
        Build(
            GradeImportErrorCodes.GradeNotFound,
            string.IsNullOrWhiteSpace(grade)
                ? "El grado del Excel está vacío o no es válido."
                : $"El grado {grade} no existe en la escuela.",
            Excel("Grado", grade),
            "Grado no encontrado o ambiguo.",
            "Use 7, 8, 9, 10, 11 o 12.");

    public static GradeImportValidationError GroupNotFound(string group, string grade) =>
        Build(
            GradeImportErrorCodes.GroupNotFound,
            string.IsNullOrWhiteSpace(group)
                ? "El grupo está vacío."
                : $"El grupo {group} no existe para el grado {Blank(grade)}.",
            Excel("Grupo", group, "Grado", grade),
            "Grupo no encontrado en el catálogo.",
            "Use el nombre exacto de Celosan, por ejemplo 7-A.");

    public static GradeImportValidationError ShiftNotFound(string shift) =>
        Build(
            GradeImportErrorCodes.ShiftNotFound,
            string.IsNullOrWhiteSpace(shift)
                ? "La jornada está vacía."
                : $"La jornada {shift} no existe en la escuela.",
            Excel("Jornada", shift),
            "Jornada no encontrada o ambigua.",
            "Use el nombre exacto, por ejemplo Noche.");

    public static GradeImportValidationError EnrollmentNotFound(
        string year, string grade, string group, string shift, string found) =>
        Build(
            GradeImportErrorCodes.EnrollmentNotFound,
            $"No existe matrícula {Blank(year)} para grado {Blank(grade)}, grupo {Blank(group)}, jornada {Blank(shift)}.",
            Excel("Año", year, "Grado", grade, "Grupo", group, "Jornada", shift),
            string.IsNullOrWhiteSpace(found) ? "Ninguna matrícula coincide." : found,
            "Revise grado, grupo y jornada del Excel. No se cambia el grado automáticamente.");

    public static GradeImportValidationError EnrollmentAmbiguous(
        int count, string year, string grade, string group, string shift) =>
        Build(
            GradeImportErrorCodes.EnrollmentAmbiguous,
            $"Se encontraron {count} matrículas coincidentes para {Blank(group)}, {Blank(shift)}, {Blank(year)}. Revise duplicidad.",
            Excel("Año", year, "Grado", grade, "Grupo", group, "Jornada", shift),
            $"{count} matrículas con la misma clave.",
            "Revise matrículas duplicadas del estudiante. No se elige una al azar.");

    public static GradeImportValidationError SubjectNotFound(string subject) =>
        Build(
            GradeImportErrorCodes.SubjectNotFound,
            string.IsNullOrWhiteSpace(subject)
                ? "La asignatura está vacía."
                : $"La asignatura {subject} no se reconoció.",
            Excel("Asignatura", subject),
            "Asignatura vacía o desconocida.",
            "Escriba el nombre exacto de la materia.");

    public static GradeImportValidationError SubjectAmbiguous(string subject) =>
        Build(
            GradeImportErrorCodes.SubjectAmbiguous,
            $"Hay más de una inscripción con el nombre {Blank(subject)}.",
            Excel("Asignatura", subject),
            "Varias inscripciones con el mismo nombre.",
            "Revise inscripciones duplicadas de esa asignatura.");

    public static GradeImportValidationError SsaNotFound(string subject, string grade, string group) =>
        Build(
            GradeImportErrorCodes.SsaNotFound,
            $"Matrícula válida encontrada, pero el estudiante no está inscrito en {Blank(subject)}.",
            Excel("Asignatura", subject, "Grado", grade, "Grupo", group),
            "Matrícula sí; inscripción de la asignatura no.",
            "Inscriba la asignatura en esa matrícula o corrija el nombre en el Excel.");

    public static GradeImportValidationError SsaInactive(string subject) =>
        Build(
            GradeImportErrorCodes.SsaInactive,
            $"La inscripción de {Blank(subject)} existe, pero está histórica/inactiva.",
            Excel("Asignatura", subject),
            "Inscripción inactiva en una matrícula activa.",
            "Reactive la inscripción o importe sobre la matrícula histórica correcta.");

    public static GradeImportValidationError InvalidTrimester(string trimester) =>
        Build(
            GradeImportErrorCodes.InvalidTrimester,
            $"El trimestre {Blank(trimester)} no es válido en la escuela.",
            Excel("Trimestre", trimester),
            "Trimestre no encontrado o ambiguo.",
            "Use 1T, 2T o 3T.");

    public static GradeImportValidationError InvalidScore(string raw, string? detail = null) =>
        Build(
            GradeImportErrorCodes.InvalidScore,
            string.IsNullOrWhiteSpace(raw)
                ? (detail ?? "La fila no tiene una nota válida para importar.")
                : (detail ?? $"La nota {raw} no es válida. Valores permitidos: 1.0–5.0, N/A o SN."),
            Excel("Nota", raw),
            "La nota no se puede guardar.",
            "Corrija la nota en el Excel.");

    public static GradeImportValidationError DuplicateExcelRow(string subject, string trimester) =>
        Build(
            GradeImportErrorCodes.DuplicateExcelRow,
            "La misma asignatura y trimestre aparecen más de una vez en el archivo.",
            Excel("Asignatura", subject, "Trimestre", trimester),
            "Filas duplicadas en el Excel.",
            "Deje una sola fila por estudiante, asignatura y trimestre.");

    public static GradeImportValidationError ImportConflict() =>
        Build(
            GradeImportErrorCodes.ImportConflict,
            "La información académica cambió durante la importación. Vuelva a analizar el archivo.",
            "Lote en confirmación",
            "Los datos académicos cambiaron después del preview.",
            "Vuelva a analizar el archivo.");

    public static GradeImportValidationError Unknown() =>
        Build(
            GradeImportErrorCodes.UnknownValidationError,
            "No se pudo validar esta fila. Revise los datos o vuelva a analizar el archivo.",
            "—",
            "Error interno controlado.",
            "Revise documento, grado, grupo y asignatura. Si persiste, reporte el código UNKNOWN_VALIDATION_ERROR.");

    private static GradeImportValidationError Build(
        string code, string message, string excel, string found, string review) =>
        new()
        {
            Code = code,
            Message = message,
            ExcelValue = excel,
            Found = found,
            Review = review
        };

    private static string Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string Excel(params string[] pairs)
    {
        var parts = new List<string>();
        for (var i = 0; i + 1 < pairs.Length; i += 2)
            parts.Add($"{pairs[i]}: {Blank(pairs[i + 1])}");
        return string.Join(" · ", parts);
    }
}
