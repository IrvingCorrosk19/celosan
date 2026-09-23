using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Dtos;
using System;
using System.Threading.Tasks;
using SchoolManager.Services.Implementations;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Net;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "student,estudiante,admin,director,secretaria,acudiente,parent")]
public class StudentReportController : Controller
{
    private readonly IStudentReportService _reportService;
    private readonly IStudentBulletinService _bulletinService;
    private readonly IStudentBulletinPdfService _bulletinPdfService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly SchoolDbContext _context;
    private readonly ILogger<StudentReportController> _logger;

    public StudentReportController(
        IStudentReportService reportService,
        IStudentBulletinService bulletinService,
        IStudentBulletinPdfService bulletinPdfService,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        SchoolDbContext context,
        ILogger<StudentReportController> logger)
    {
        _reportService = reportService;
        _bulletinService = bulletinService;
        _bulletinPdfService = bulletinPdfService;
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            _logger.LogInformation("=== INICIO StudentReportController.Index ===");
            Console.WriteLine("=== INICIO StudentReportController.Index ===");

            // Obtener el ID del usuario autenticado
            var studentId = await _currentUserService.GetCurrentUserIdAsync();
            _logger.LogInformation("StudentId obtenido: {StudentId}", studentId);
            Console.WriteLine($"StudentId obtenido: {studentId}");

            if (studentId == null)
            {
                _logger.LogWarning("No se pudo identificar al usuario actual");
                Console.WriteLine("No se pudo identificar al usuario actual");
                return Unauthorized("No se pudo identificar al usuario actual.");
            }

            // Obtener el reporte real desde el servicio (sin pasar trimestre)
            _logger.LogInformation("Llamando a GetReportByStudentIdAsync para StudentId: {StudentId}", studentId.Value);
            Console.WriteLine($"Llamando a GetReportByStudentIdAsync para StudentId: {studentId.Value}");
            
            var report = await _reportService.GetReportByStudentIdAsync(studentId.Value);
            _logger.LogInformation("Reporte obtenido: {Report}", report != null ? "NO NULL" : "NULL");
            Console.WriteLine($"Reporte obtenido: {(report != null ? "NO NULL" : "NULL")}");

            if (report == null)
            {
                _logger.LogWarning("No se encontró reporte - estudiante sin calificaciones");
                Console.WriteLine("No se encontró reporte - estudiante sin calificaciones");
                
                // Si no hay reporte, crea un modelo vacío y agrega mensaje para SweetAlert
                report = new StudentReportDto
                {
                    StudentId = studentId.Value,
                    Grades = new List<GradeDto>(),
                    AttendanceByTrimester = new List<AttendanceDto>(),
                    AttendanceByMonth = new List<AttendanceDto>(),
                    AvailableTrimesters = new List<AvailableTrimesters>(),
                    DisciplineReports = new List<DisciplineReportDto>(),
                    StudentName = "Estudiante" // Nombre por defecto
                };
                
                // Agregar mensaje para mostrar con SweetAlert
                TempData["ShowNoGradesAlert"] = "true";
            }
            else
            {
                _logger.LogInformation("Reporte encontrado - Grades: {GradesCount}, AvailableTrimesters: {TrimestersCount}", 
                    report.Grades?.Count ?? 0, report.AvailableTrimesters?.Count ?? 0);
                Console.WriteLine($"Reporte encontrado - Grades: {report.Grades?.Count ?? 0}, AvailableTrimesters: {report.AvailableTrimesters?.Count ?? 0}");
            }

            // Forzar que el trimestre seleccionado sea 1T si existe, si no el primero disponible
            var availableTrimesters = report.AvailableTrimesters?.Select(t => t.Trimester).ToList() ?? new List<string>();
            _logger.LogInformation("Trimestres disponibles: {Trimesters}", string.Join(", ", availableTrimesters));
            Console.WriteLine($"Trimestres disponibles: {string.Join(", ", availableTrimesters)}");

            string selectedTrimester = availableTrimesters.Contains("1T") ? "1T" : availableTrimesters.FirstOrDefault() ?? "1T";
            _logger.LogInformation("Trimestre seleccionado: {SelectedTrimester}", selectedTrimester);
            Console.WriteLine($"Trimestre seleccionado: {selectedTrimester}");

            if (selectedTrimester != null && report.Trimester != selectedTrimester)
            {
                _logger.LogInformation("Solicitando reporte específico para trimestre: {Trimester}", selectedTrimester);
                Console.WriteLine($"Solicitando reporte específico para trimestre: {selectedTrimester}");
                
                // Volver a pedir el reporte solo para el trimestre seleccionado
                report = await _reportService.GetReportByStudentIdAndTrimesterAsync(studentId.Value, selectedTrimester) ?? report;
                report.AvailableTrimesters = availableTrimesters.Select(t => new AvailableTrimesters { Trimester = t }).ToList();
                
                _logger.LogInformation("Reporte específico obtenido - Grades: {GradesCount}", report.Grades?.Count ?? 0);
                Console.WriteLine($"Reporte específico obtenido - Grades: {report.Grades?.Count ?? 0}");
            }
            
            report.StudentId = studentId.Value;
            ViewBag.AvailableTrimesters = report.AvailableTrimesters;
            
            _logger.LogInformation("=== FIN StudentReportController.Index - Enviando a vista ===");
            Console.WriteLine("=== FIN StudentReportController.Index - Enviando a vista ===");
            
            return View(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en StudentReportController.Index: {Message}", ex.Message);
            Console.WriteLine($"ERROR en StudentReportController.Index: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw;
        }
    }

    
    public async Task<IActionResult> GetTrimesterData(Guid studentId, string trimester)
    {
        try
        {
            var access = await AuthorizeStudentAcademicAccessAsync(studentId);
            if (access != null)
                return access;

            _logger.LogInformation("=== INICIO GetTrimesterData - StudentId: {StudentId}, Trimester: {Trimester} ===", studentId, trimester);
            Console.WriteLine($"=== INICIO GetTrimesterData - StudentId: {studentId}, Trimester: {trimester} ===");

            var report = await _reportService.GetReportByStudentIdAndTrimesterAsync(studentId, trimester);
            _logger.LogInformation("Reporte obtenido en GetTrimesterData: {Report}", report != null ? "NO NULL" : "NULL");
            Console.WriteLine($"Reporte obtenido en GetTrimesterData: {(report != null ? "NO NULL" : "NULL")}");

            if (report == null)
            {
                _logger.LogWarning("No se encontraron datos para StudentId: {StudentId}, Trimester: {Trimester}", studentId, trimester);
                Console.WriteLine($"No se encontraron datos para StudentId: {studentId}, Trimester: {trimester}");
                return Json(new { error = "No se encontraron datos para el trimestre seleccionado." });
            }

            _logger.LogInformation("Datos encontrados - Grades: {GradesCount}, Attendance: {AttendanceCount}, Discipline: {DisciplineCount}", 
                report.Grades?.Count ?? 0, report.AttendanceByTrimester?.Count ?? 0, report.DisciplineReports?.Count ?? 0);
            Console.WriteLine($"Datos encontrados - Grades: {report.Grades?.Count ?? 0}, Attendance: {report.AttendanceByTrimester?.Count ?? 0}, Discipline: {report.DisciplineReports?.Count ?? 0}");

            // Preparar los datos de manera segura
            object MapGrade(GradeDto g) => new
            {
                subject = g.Subject,
                activityName = g.ActivityName,
                teacher = g.Teacher,
                value = g.Value,
                fileUrl = g.FileUrl,
                type = g.Type,
                groupContext = g.GroupContext,
                levelContext = g.LevelContext,
                isCarryOver = g.IsCarryOver
            };

            var grades = report.Grades?.Select(MapGrade).Cast<object>().ToList() ?? new List<object>();
            var carryOverGrades = report.CarryOverGrades?.Select(MapGrade).Cast<object>().ToList() ?? new List<object>();
            var activeEnrollments = report.ActiveEnrollments?.Select(e => new
            {
                gradeName = e.GradeName,
                groupName = e.GroupName,
                shiftName = e.ShiftName,
                enrollmentType = e.EnrollmentType,
                isPrimary = e.IsPrimary,
                isCarryOver = e.IsCarryOver
            }).Cast<object>().ToList() ?? new List<object>();

            var attendanceByTrimester = new List<object>();
            if (report.AttendanceByTrimester != null)
            {
                attendanceByTrimester = report.AttendanceByTrimester.Select(a => new {
                    trimester = a.Trimester,
                    present = a.Present,
                    absent = a.Absent,
                    late = a.Late
                }).Cast<object>().ToList();
            }

            var attendanceByMonth = new List<object>();
            if (report.AttendanceByMonth != null)
            {
                attendanceByMonth = report.AttendanceByMonth.Select(a => new {
                    month = a.Month,
                    present = a.Present,
                    absent = a.Absent,
                    late = a.Late
                }).Cast<object>().ToList();
            }

            var disciplineReports = new List<object>();
            if (report.DisciplineReports != null)
            {
                disciplineReports = report.DisciplineReports.Select(r => new {
                    type = r.Type ?? "",
                    status = r.Status ?? "",
                    description = r.Description ?? "",
                    date = r.Date.ToString("yyyy-MM-dd"),
                    time = r.Time ?? "",
                    teacher = r.Teacher ?? ""
                }).Cast<object>().ToList();
            }

            var pendingActivities = new List<object>();
            if (report.PendingActivities != null)
            {
                pendingActivities = report.PendingActivities.Select(a => new {
                    activityId = a.ActivityId,
                    name = a.Name,
                    type = a.Type,
                    subjectName = a.SubjectName,
                    createdAt = a.CreatedAt.ToString("yyyy-MM-dd"),
                    fileUrl = a.FileUrl,
                    teacherName = a.TeacherName
                }).Cast<object>().ToList();
            }

            var subjectAverages = report.SubjectAverages?.Select(a => new
            {
                subject = a.Subject,
                averageApreciacion = a.AverageApreciacion,
                averageEjercicios = a.AverageEjercicios,
                averageExamen = a.AverageExamen,
                subjectAverage = a.SubjectAverage,
                isImported = a.IsImported,
                gradeOrigin = a.GradeOrigin
            }).Cast<object>().ToList() ?? new List<object>();

            var result = new
            {
                grades = grades,
                carryOverGrades = carryOverGrades,
                activeEnrollments = activeEnrollments,
                trimester = report.Trimester,
                subjectAverages = subjectAverages,
                trimesterAverage = report.TrimesterAverage,
                attendanceByTrimester = attendanceByTrimester,
                attendanceByMonth = attendanceByMonth,
                disciplineReports = disciplineReports,
                pendingActivities = pendingActivities
            };

            _logger.LogInformation("=== FIN GetTrimesterData - Enviando respuesta ===");
            Console.WriteLine("=== FIN GetTrimesterData - Enviando respuesta ===");

            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetTrimesterData: {Message}", ex.Message);
            Console.WriteLine($"ERROR en GetTrimesterData: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return Json(new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetBulletin(Guid? studentId)
    {
        var resolved = await ResolveBulletinStudentAsync(studentId);
        if (resolved.Error != null)
            return resolved.Error;

        var bulletin = await _bulletinService.GetByGradeBulletinAsync(resolved.TargetId);
        if (bulletin == null)
            return NotFound(new { error = "No se encontró el boletín." });

        return Json(bulletin);
    }

    [HttpGet]
    public async Task<IActionResult> GetBulletinHistory(Guid? studentId)
    {
        var resolved = await ResolveBulletinStudentAsync(studentId);
        if (resolved.Error != null)
            return resolved.Error;

        var history = await _bulletinService.GetProgramHistoryAsync(resolved.TargetId);
        if (history == null)
            return NotFound(new { error = "No se encontró el historial del boletín." });

        return Json(history);
    }

    [HttpGet]
    public async Task<IActionResult> ExportBulletinPdf(string? view, Guid? studentId)
    {
        var resolved = await ResolveBulletinStudentAsync(studentId);
        if (resolved.Error != null)
            return resolved.Error;

        var school = await ResolveBulletinSchoolAsync(resolved.TargetId);
        var identity = await _bulletinPdfService.BuildIdentityAsync(school?.Name, school?.LogoUrl);
        var isProgram = IsProgramBulletinView(view);

        if (isProgram)
        {
            var history = await _bulletinService.GetProgramHistoryAsync(resolved.TargetId);
            if (history == null)
                return NotFound(new { error = "No se encontró el historial del boletín." });

            var year = history.Tracks
                .SelectMany(t => t.Grades ?? new List<ProgramHistoryGradeColumnDto>())
                .Select(g => g.AcademicYear)
                .FirstOrDefault(y => !string.IsNullOrWhiteSpace(y) && y != "—");
            var bytes = _bulletinPdfService.GenerateProgramHistoryPdf(history, identity);
            var fileName = _bulletinPdfService.BuildFileName(history.StudentName, year, isProgramComplete: true);
            return File(bytes, "application/pdf", fileName);
        }

        var bulletin = await _bulletinService.GetByGradeBulletinAsync(resolved.TargetId);
        if (bulletin == null)
            return NotFound(new { error = "No se encontró el boletín." });

        var pdf = _bulletinPdfService.GenerateByGradePdf(bulletin, identity);
        var gradeFileName = _bulletinPdfService.BuildFileName(bulletin.StudentName, bulletin.AcademicYear, isProgramComplete: false);
        return File(pdf, "application/pdf", gradeFileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportDisciplinePdf(Guid studentId, string studentName, string grade)
    {
        try
        {
            var access = await AuthorizeStudentAcademicAccessAsync(studentId);
            if (access != null)
                return access;

            _logger.LogInformation("=== INICIO ExportDisciplinePdf - StudentId: {StudentId} ===", studentId);
            Console.WriteLine($"=== INICIO ExportDisciplinePdf - StudentId: {studentId} ===");

            // Obtener todos los reportes de disciplina del estudiante
            var disciplineReports = await _reportService.GetDisciplineReportsByStudentIdAsync(studentId);
            
            _logger.LogInformation("Reportes de disciplina encontrados: {Count}", disciplineReports?.Count ?? 0);
            Console.WriteLine($"Reportes de disciplina encontrados: {disciplineReports?.Count ?? 0}");

            // Generar HTML para el PDF
            var htmlContent = GenerateDisciplineReportHtml(studentName, grade, disciplineReports);

            // Convertir HTML a PDF (usando un enfoque simple con HTML)
            // Usar UTC para nombres de archivo para consistencia
            var fileName = $"Expediente_Disciplina_{studentName?.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.html";
            
            _logger.LogInformation("=== FIN ExportDisciplinePdf - Archivo generado: {FileName} ===", fileName);
            Console.WriteLine($"=== FIN ExportDisciplinePdf - Archivo generado: {fileName} ===");

            // Retornar el archivo HTML (que se puede convertir a PDF en el navegador)
            var bytes = Encoding.UTF8.GetBytes(htmlContent);
            return File(bytes, "text/html", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ExportDisciplinePdf: {Message}", ex.Message);
            Console.WriteLine($"ERROR en ExportDisciplinePdf: {ex.Message}");
            return BadRequest($"Error al generar el expediente: {ex.Message}");
        }
    }

    private string GenerateDisciplineReportHtml(string studentName, string grade, List<DisciplineReportDto> reports)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='es'>");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset='UTF-8'>");
        html.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        html.AppendLine("    <title>Expediente de Disciplina</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("        .header { text-align: center; margin-bottom: 30px; border-bottom: 2px solid #333; padding-bottom: 20px; }");
        html.AppendLine("        .header h1 { color: #2563eb; margin: 0; }");
        html.AppendLine("        .header h2 { color: #666; margin: 10px 0; }");
        html.AppendLine("        .info { margin-bottom: 30px; }");
        html.AppendLine("        .info p { margin: 5px 0; }");
        html.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
        html.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
        html.AppendLine("        th { background-color: #f8f9fa; font-weight: bold; }");
        html.AppendLine("        .no-data { text-align: center; color: #666; font-style: italic; padding: 20px; }");
        html.AppendLine("        .footer { margin-top: 30px; text-align: center; color: #666; font-size: 12px; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Header
        html.AppendLine("    <div class='header'>");
        html.AppendLine("        <h1>EXPEDIENTE DE DISCIPLINA</h1>");
        html.AppendLine("        <h2>Historial de Reportes de Disciplina</h2>");
        html.AppendLine("    </div>");
        
        // Información del estudiante
        html.AppendLine("    <div class='info'>");
        html.AppendLine("        <p><strong>Estudiante:</strong> " + (studentName ?? "N/A") + "</p>");
        html.AppendLine("        <p><strong>Grado:</strong> " + (grade ?? "N/A") + "</p>");
        // Convertir UTC a hora local para mostrar al usuario
        var fechaGeneracion = DateTime.UtcNow.ToLocalTime();
        html.AppendLine("        <p><strong>Fecha de Generación:</strong> " + fechaGeneracion.ToString("dd/MM/yyyy HH:mm") + "</p>");
        html.AppendLine("    </div>");
        
        // Tabla de reportes
        html.AppendLine("    <table>");
        html.AppendLine("        <thead>");
        html.AppendLine("            <tr>");
        html.AppendLine("                <th>Fecha</th>");
        html.AppendLine("                <th>Hora</th>");
        html.AppendLine("                <th>Tipo</th>");
        html.AppendLine("                <th>Estado</th>");
        html.AppendLine("                <th>Acciones</th>");
        html.AppendLine("                <th>Descripción</th>");
        html.AppendLine("                <th>Profesor</th>");
        html.AppendLine("            </tr>");
        html.AppendLine("        </thead>");
        html.AppendLine("        <tbody>");
        
        if (reports != null && reports.Any())
        {
            foreach (var report in reports)
            {
                var fecha = report.Date.ToString("dd/MM/yyyy");
                var hora = report.Time ?? "Sin hora";
                var tipo = report.Type ?? "Sin tipo";
                var estado = report.Status ?? "Sin estado";
                var descripcion = report.Description ?? "Sin descripción";
                var profesor = report.Teacher ?? "Sin profesor";
                var acciones = FormatDisciplineActionsForHtml(report.DisciplineActionsJson);
                
                html.AppendLine("            <tr>");
                html.AppendLine($"                <td>{fecha}</td>");
                html.AppendLine($"                <td>{hora}</td>");
                html.AppendLine($"                <td>{tipo}</td>");
                html.AppendLine($"                <td>{estado}</td>");
                html.AppendLine($"                <td>{acciones}</td>");
                html.AppendLine($"                <td>{descripcion}</td>");
                html.AppendLine($"                <td>{profesor}</td>");
                html.AppendLine("            </tr>");
            }
        }
        else
        {
            html.AppendLine("            <tr>");
            html.AppendLine("                <td colspan='7' class='no-data'>No hay reportes de disciplina registrados</td>");
            html.AppendLine("            </tr>");
        }
        
        html.AppendLine("        </tbody>");
        html.AppendLine("    </table>");
        
        // Footer
        html.AppendLine("    <div class='footer'>");
        html.AppendLine("        <p>Este documento fue generado automáticamente por el sistema EduPlanner</p>");
        html.AppendLine("        <p>Para convertir a PDF, use la función 'Imprimir' de su navegador y seleccione 'Guardar como PDF'</p>");
        html.AppendLine("    </div>");
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    private static string FormatDisciplineActionsForHtml(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "—";
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list == null || list.Count == 0) return "—";
            var text = string.Join("; ", list.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrEmpty(text) ? "—" : WebUtility.HtmlEncode(text);
        }
        catch
        {
            return "—";
        }
    }

    private async Task<(Guid TargetId, IActionResult? Error)> ResolveBulletinStudentAsync(Guid? studentId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return (Guid.Empty, Unauthorized());

        var role = currentUser.Role?.ToLowerInvariant() ?? "";
        Guid targetId;
        if (role is "student" or "estudiante")
        {
            targetId = currentUser.Id;
        }
        else if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            targetId = studentId.Value;
        }
        else
        {
            return (Guid.Empty, BadRequest(new { error = "Debe indicar el estudiante." }));
        }

        var access = await AuthorizeStudentAcademicAccessAsync(targetId);
        if (access != null)
            return (Guid.Empty, access);

        return (targetId, null);
    }

    /// <summary>
    /// Identidad institucional del PDF: escuela del estudiante (SchoolId).
    /// La escuela del usuario autenticado solo se usa si el estudiante no tiene SchoolId.
    /// No se cruza a otra escuela cuando el estudiante ya tiene tenant.
    /// </summary>
    private async Task<School?> ResolveBulletinSchoolAsync(Guid studentId)
    {
        var studentSchoolId = await _context.Users.AsNoTracking()
            .Where(u => u.Id == studentId)
            .Select(u => u.SchoolId)
            .FirstOrDefaultAsync();

        if (studentSchoolId.HasValue)
        {
            var studentSchool = await _context.Schools.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentSchoolId.Value);
            if (studentSchool != null)
                return studentSchool;

            _logger.LogWarning(
                "Boletín PDF: el estudiante {StudentId} tiene SchoolId {SchoolId} pero no se encontró la escuela. No se usa otra institución.",
                studentId, studentSchoolId);
            return null;
        }

        var userSchool = await _currentUserService.GetCurrentUserSchoolAsync();
        if (userSchool != null)
        {
            _logger.LogInformation(
                "Boletín PDF: el estudiante {StudentId} no tiene SchoolId; se usa la escuela del usuario autenticado {SchoolId} como fallback.",
                studentId, userSchool.Id);
        }

        return userSchool;
    }

    private static bool IsProgramBulletinView(string? view)
    {
        var value = (view ?? string.Empty).Trim();
        return value.Equals("program", StringComparison.OrdinalIgnoreCase)
            || value.Equals("history", StringComparison.OrdinalIgnoreCase)
            || value.Equals("programa", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IActionResult?> AuthorizeStudentAcademicAccessAsync(Guid studentId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var role = currentUser.Role?.ToLowerInvariant() ?? "";
        if (role is "student" or "estudiante")
            return currentUser.Id != studentId ? Forbid() : null;

        if (role is "parent" or "acudiente")
        {
            if (!await IsParentOfStudentAsync(currentUser.Id, studentId, currentUser.SchoolId))
                return Forbid();
            return null;
        }

        if (!await SchoolTenantHelper.StudentBelongsToTenantAsync(_context, _tenantContext, studentId))
            return Forbid();

        return null;
    }

    private async Task<bool> IsParentOfStudentAsync(Guid parentId, Guid studentId, Guid? schoolId)
    {
        return await _context.Prematriculations.AsNoTracking()
            .Where(p => p.ParentId == parentId && p.StudentId == studentId)
            .Join(_context.Users.AsNoTracking(),
                p => p.StudentId,
                u => u.Id,
                (p, u) => u)
            .AnyAsync(u =>
                (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante") &&
                (!schoolId.HasValue || u.SchoolId == schoolId));
    }

}
