using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class GradeImportErrorFactoryTests
{
    [Fact]
    public void Enrollment_not_found_names_year_grade_group_and_shift()
    {
        var error = GradeImportErrorFactory.EnrollmentNotFound("2026", "8", "8-A", "Noche", string.Empty);

        Assert.Equal(GradeImportErrorCodes.EnrollmentNotFound, error.Code);
        Assert.Equal("No existe matrícula 2026 para grado 8, grupo 8-A, jornada Noche.", error.Message);
        Assert.Contains("8-A", error.ExcelValue);
        Assert.DoesNotContain("guid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enrollment_ambiguous_includes_count()
    {
        var error = GradeImportErrorFactory.EnrollmentAmbiguous(2, "2026", "7", "7-A", "Noche");

        Assert.Equal(GradeImportErrorCodes.EnrollmentAmbiguous, error.Code);
        Assert.Equal("Se encontraron 2 matrículas coincidentes para 7-A, Noche, 2026. Revise duplicidad.", error.Message);
    }

    [Fact]
    public void Ssa_not_found_keeps_enrollment_and_subject()
    {
        var error = GradeImportErrorFactory.SsaNotFound("CIENCIAS NATURALES", "7", "7-A");

        Assert.Equal(GradeImportErrorCodes.SsaNotFound, error.Code);
        Assert.Equal(
            "Matrícula válida encontrada, pero el estudiante no está inscrito en CIENCIAS NATURALES.",
            error.Message);
    }

    [Fact]
    public void Ssa_inactive_is_distinct_from_missing()
    {
        var error = GradeImportErrorFactory.SsaInactive("CIENCIAS NATURALES");

        Assert.Equal(GradeImportErrorCodes.SsaInactive, error.Code);
        Assert.Equal(
            "La inscripción de CIENCIAS NATURALES existe, pero está histórica/inactiva.",
            error.Message);
    }

    [Fact]
    public void Group_not_found_mentions_grade()
    {
        var error = GradeImportErrorFactory.GroupNotFound("9-A", "9");

        Assert.Equal(GradeImportErrorCodes.GroupNotFound, error.Code);
        Assert.Equal("El grupo 9-A no existe para el grado 9.", error.Message);
    }

    [Fact]
    public void Invalid_score_lists_allowed_values()
    {
        var error = GradeImportErrorFactory.InvalidScore("6.0");

        Assert.Equal(GradeImportErrorCodes.InvalidScore, error.Code);
        Assert.Equal("La nota 6.0 no es válida. Valores permitidos: 1.0–5.0, N/A o SN.", error.Message);
    }

    [Fact]
    public void Duplicate_row_does_not_expose_technical_ids()
    {
        var error = GradeImportErrorFactory.DuplicateExcelRow("CIENCIAS NATURALES", "1T");

        Assert.Equal(GradeImportErrorCodes.DuplicateExcelRow, error.Code);
        Assert.Equal("La misma asignatura y trimestre aparecen más de una vez en el archivo.", error.Message);
        Assert.DoesNotContain("{", error.Message);
    }

    [Fact]
    public void Student_mismatch_uses_requested_wording()
    {
        var error = GradeImportErrorFactory.StudentIdEmailMismatch(
            "8-1-1", "a@x.com", "El documento y el email corresponden a estudiantes distintos.");

        Assert.Equal(GradeImportErrorCodes.StudentIdEmailMismatch, error.Code);
        Assert.Equal("El documento y el email corresponden a estudiantes distintos.", error.Message);
    }

    [Fact]
    public void Codes_cover_student_enrollment_group_shift_subject_ssa_score_and_duplicate()
    {
        var codes = new[]
        {
            GradeImportErrorFactory.StudentNotFound("1", "a@x.com").Code,
            GradeImportErrorFactory.EnrollmentNotFound("2026", "8", "8-A", "Noche", "").Code,
            GradeImportErrorFactory.EnrollmentAmbiguous(2, "2026", "7", "7-A", "Noche").Code,
            GradeImportErrorFactory.GroupNotFound("A", "7").Code,
            GradeImportErrorFactory.ShiftNotFound("Diurna").Code,
            GradeImportErrorFactory.SubjectNotFound("").Code,
            GradeImportErrorFactory.SsaNotFound("CIENCIAS NATURALES", "7", "7-A").Code,
            GradeImportErrorFactory.SsaInactive("CIENCIAS NATURALES").Code,
            GradeImportErrorFactory.InvalidScore("6.0").Code,
            GradeImportErrorFactory.DuplicateExcelRow("X", "1T").Code,
            GradeImportErrorFactory.Unknown().Code
        };

        Assert.Contains(GradeImportErrorCodes.StudentNotFound, codes);
        Assert.Contains(GradeImportErrorCodes.EnrollmentNotFound, codes);
        Assert.Contains(GradeImportErrorCodes.EnrollmentAmbiguous, codes);
        Assert.Contains(GradeImportErrorCodes.GroupNotFound, codes);
        Assert.Contains(GradeImportErrorCodes.ShiftNotFound, codes);
        Assert.Contains(GradeImportErrorCodes.SubjectNotFound, codes);
        Assert.Contains(GradeImportErrorCodes.SsaNotFound, codes);
        Assert.Contains(GradeImportErrorCodes.SsaInactive, codes);
        Assert.Contains(GradeImportErrorCodes.InvalidScore, codes);
        Assert.Contains(GradeImportErrorCodes.DuplicateExcelRow, codes);
        Assert.Contains(GradeImportErrorCodes.UnknownValidationError, codes);
    }
}
