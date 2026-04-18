namespace SchoolManager.ViewModels;

/// <summary>
/// Carga masiva unificada: matrícula grado/grupo o inscripciones por materia (misma acción HTTP).
/// </summary>
public class BulkStudentUploadRequest
{
    /// <summary>gradeGroup = Excel matrícula (grado/grupo). subjects = Excel asignaturas individuales.</summary>
    public string Mode { get; set; } = "gradeGroup";

    public List<StudentAssignmentInputModel>? GradeGroupRows { get; set; }

    public List<StudentSubjectEnrollmentInputModel>? SubjectRows { get; set; }
}
