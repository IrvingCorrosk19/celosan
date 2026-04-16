using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SchoolManager.Models;
using SchoolManager.Dtos;
using SchoolManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SchoolManager.Services.Interfaces;
using SchoolManager.Interfaces;

[Authorize(Roles = "teacher")]
public class OrientationReportController : Controller
{
    private readonly IOrientationReportService _orientationReportService;
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrientationReportController> _logger;
    private readonly ITeacherGroupService _teacherGroupService;
    private readonly IGradeLevelService _gradeLevelService;
    private readonly ISubjectService _subjectService;
    private readonly ITrimesterService _trimesterService;
    private readonly IActivityTypeService _activityTypeService;
    private readonly IStudentService _studentService;
    private readonly IAttendanceService _attendanceService;
    private readonly ICurrentUserService _currentUserService;

    public OrientationReportController(
        IOrientationReportService orientationReportService, 
        IUserService userService,
        IEmailService emailService,
        ILogger<OrientationReportController> logger,
        ITeacherGroupService teacherGroupService,
        IGradeLevelService gradeLevelService,
        ISubjectService subjectService,
        ITrimesterService trimesterService,
        IActivityTypeService activityTypeService,
        IStudentService studentService,
        IAttendanceService attendanceService,
        ICurrentUserService currentUserService)
    {
        _orientationReportService = orientationReportService;
        _userService = userService;
        _emailService = emailService;
        _logger = logger;
        _teacherGroupService = teacherGroupService;
        _gradeLevelService = gradeLevelService;
        _subjectService = subjectService;
        _trimesterService = trimesterService;
        _activityTypeService = activityTypeService;
        _studentService = studentService;
        _attendanceService = attendanceService;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Index()
    {
        var teacherId = GetTeacherId();

        // 🔹 Obtener todos los docentes con asignaciones
        var teachers = await _userService.GetAllWithAssignmentsByRoleAsync("teacher");
        var teacher = teachers.FirstOrDefault(t => t.Id == teacherId);

        if (teacher == null)
            return NotFound("No se encontró el docente actual.");

        // 🔹 Obtener TODAS las asignaciones de TODOS los docentes (sin filtrar por ID)
        var allAssignments = teachers
            .Where(t => t.TeacherAssignments != null)
            .SelectMany(t => t.TeacherAssignments)
            .ToList();

        var subjectGroupDetails = allAssignments.Any()
           ? allAssignments
               .GroupBy(a => new
               {
                   SubjectId = a.SubjectAssignment.SubjectId,
                   SubjectName = a.SubjectAssignment.Subject?.Name ?? "(Sin materia)"
               })
               .Select(g => new SubjectGroupSummary
               {
                   SubjectId = g.Key.SubjectId,
                   SubjectName = g.Key.SubjectName,
                   GroupGradePairs = g.Select(x => new GroupGradeItem
                   {
                       GroupId = x.SubjectAssignment.GroupId,
                       GradeLevelId = x.SubjectAssignment.GradeLevelId,
                       ShiftId = x.SubjectAssignment.Group != null ? x.SubjectAssignment.Group.ShiftId : null,
                       GroupName = x.SubjectAssignment.Group?.Name ?? "(Grupo)",
                       GradeLevelName = x.SubjectAssignment.GradeLevel?.Name ?? "(Grado)"
                   })
                   .Distinct()
                   .ToList()
               }).ToList()
           : new List<SubjectGroupSummary>();

        var teacherInfo = new TeacherAssignmentDisplayDto
        {
            TeacherId = teacher.Id,
            FullName = $"{teacher.Name} {teacher.LastName}",
            Email = teacher.Email,
            Role = teacher.Role,
            IsActive = string.Equals(teacher.Status, "active", StringComparison.OrdinalIgnoreCase),
            SubjectGroupDetails = subjectGroupDetails
        };

        // 🔹 Obtener catálogo para filtros
        var trimesters = (await _trimesterService.GetAllAsync()).ToList();
        var firstTrim = trimesters.FirstOrDefault()?.Name ?? "";
        var groups = await _teacherGroupService.GetByTeacherAsync(teacherId, firstTrim);
        var types = await _activityTypeService.GetAllAsync();

        // 🔹 ViewModel que combinamos con datos del docente y catálogos
        var viewModel = new TeacherGradebookViewModel
        {
            Teacher = teacherInfo,
            Trimesters = trimesters,
            Groups = groups,
            Types = types,
            TeacherId = teacherId
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var report = await _orientationReportService.GetByIdAsync(id);
        if (report == null) return NotFound();
        return View(report);
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> CreateWithFiles()
    {
        try
        {
            // Obtener datos del formulario
            var studentId = Request.Form["StudentId"].ToString();
            var teacherId = Request.Form["TeacherId"].ToString();
            var subjectId = Request.Form["SubjectId"].ToString();
            var groupId = Request.Form["GroupId"].ToString();
            var gradeLevelId = Request.Form["GradeLevelId"].ToString();
            var date = Request.Form["Date"].ToString();
            var hora = Request.Form["Hora"].ToString();
            var reportType = Request.Form["ReportType"].ToString();
            var status = Request.Form["Status"].ToString();
            var description = Request.Form["Description"].ToString();
            var category = Request.Form["Category"].ToString();

            // Validar datos requeridos
            if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(teacherId) || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(hora))
            {
                return Json(new { success = false, error = "Datos requeridos faltantes" });
            }

            // Procesar archivos si existen
            var documentsJson = "";
            var files = Request.Form.Files.Where(f => f.Name == "Documents").ToList();
            if (files.Any())
            {
                var documentList = new List<object>();
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        // Crear directorio si no existe
                        var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "orientation");
                        if (!Directory.Exists(uploadsPath))
                        {
                            Directory.CreateDirectory(uploadsPath);
                        }

                        // Generar nombre único para el archivo
                        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                        var filePath = Path.Combine(uploadsPath, fileName);

                        // Guardar archivo
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        documentList.Add(new
                        {
                            fileName = file.FileName,
                            savedName = fileName,
                            size = file.Length,
                            uploadDate = DateTime.UtcNow
                        });
                    }
                }
                documentsJson = System.Text.Json.JsonSerializer.Serialize(documentList);
            }

            // Obtener usuario autenticado y su school_id
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
            
            var orientationReport = new OrientationReport
            {
                Id = Guid.NewGuid(),
                SchoolId = currentUser?.SchoolId, // ✅ SchoolId del usuario autenticado
                StudentId = Guid.Parse(studentId),
                TeacherId = Guid.Parse(teacherId),
                SubjectId = !string.IsNullOrEmpty(subjectId) ? Guid.Parse(subjectId) : (Guid?)null,
                GroupId = !string.IsNullOrEmpty(groupId) ? Guid.Parse(groupId) : (Guid?)null,
                GradeLevelId = !string.IsNullOrEmpty(gradeLevelId) ? Guid.Parse(gradeLevelId) : (Guid?)null,
                Date = DateTime.SpecifyKind(DateTime.Parse($"{date} {hora}"), DateTimeKind.Local).ToUniversalTime(),
                ReportType = reportType,
                Status = status,
                Description = description,
                Category = category,
                Documents = documentsJson,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId, // ✅ ID del usuario autenticado
                UpdatedBy = currentUserId  // ✅ ID del usuario autenticado
            };

            try
            {
                await _orientationReportService.CreateAsync(orientationReport);
                return Json(new { success = true, message = "Registro guardado correctamente", orientationReportId = orientationReport.Id });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error al guardar en la base de datos");
                return Json(new { success = false, error = "Error al guardar en la base de datos", details = ex.InnerException?.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear el reporte de orientación");
            return Json(new { success = false, error = "Error al crear el reporte", details = ex.Message });
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var report = await _orientationReportService.GetByIdAsync(id);
        if (report == null) return NotFound();
        return View(report);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(OrientationReport report)
    {
        if (ModelState.IsValid)
        {
            await _orientationReportService.UpdateAsync(report);
            return RedirectToAction(nameof(Index));
        }
        return View(report);
    }

    public async Task<IActionResult> Delete(Guid id)
    {
        var report = await _orientationReportService.GetByIdAsync(id);
        if (report == null) return NotFound();
        return View(report);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        await _orientationReportService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetByStudent(Guid studentId)
    {
        var reports = await _orientationReportService.GetByStudentDtoAsync(studentId);
        return Json(reports.Select(r => new {
            date = r.Date,
            time = r.Date.ToString("HH:mm"),
            type = r.Type,
            categoria = r.Category,
            status = r.Status,
            description = r.Description,
            documents = r.Documents,
            teacher = r.Teacher,
            subjectId = r.SubjectId, // ✅ Agregado
            subjectName = r.SubjectName // ✅ Agregado
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetFiltered(DateTime? fechaInicio, DateTime? fechaFin, Guid? gradoId, Guid? groupId = null, Guid? studentId = null)
    {
        if (!gradoId.HasValue)
        {
            return BadRequest(new { error = "El grado es obligatorio" });
        }

        try
        {
            var reports = await _orientationReportService.GetFilteredAsync(fechaInicio, fechaFin, gradoId, groupId, studentId);
            
            var result = reports.Select(r => new {
                estudiante = r.Student != null ? $"{r.Student.Name} {r.Student.LastName}" : null,
                documentId = r.Student?.DocumentId,
                fecha = r.Date.ToString("dd/MM/yyyy"),
                hora = r.Date.ToString("HH:mm"),
                tipo = r.ReportType,
                categoria = r.Category,
                status = r.Status,
                description = r.Description,
                documents = r.Documents,
                grupo = r.Group?.Name,
                grado = r.GradeLevel?.Name
            });

            return Json(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportToExcel(DateTime? fechaInicio, DateTime? fechaFin, Guid? gradoId)
    {
        var reports = await _orientationReportService.GetFilteredAsync(fechaInicio, fechaFin, gradoId);
        var csv = "Estudiante,Fecha,Tipo,Estado,Descripción\n" +
            string.Join("\n", reports.Select(r => $"{(r.Student != null ? r.Student.Name : "")},{r.Date:yyyy-MM-dd},{r.ReportType},{r.Status},{r.Description}"));
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "registros_orientacion.csv");
    }

    [HttpPost]
    public async Task<IActionResult> SendEmailToStudent([FromBody] SendOrientationEmailDto request)
    {
        try
        {
            if (request.StudentId == Guid.Empty || request.OrientationReportId == Guid.Empty)
            {
                return Json(new { success = false, message = "ID de estudiante y reporte son requeridos" });
            }

            var success = await _emailService.SendOrientationReportEmailAsync(
                request.StudentId, 
                request.OrientationReportId, 
                request.CustomMessage ?? "");

            if (success)
            {
                return Json(new { success = true, message = "Correo enviado exitosamente al estudiante" });
            }
            else
            {
                return Json(new { success = false, message = "Error al enviar el correo. Verifique que el estudiante tenga email configurado y que la configuración SMTP esté activa." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo de orientación");
            return Json(new { success = false, message = "Error interno del servidor al enviar el correo" });
        }
    }

    private Guid GetTeacherId()
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        if (!Guid.TryParse(userId, out var teacherId))
        {
            throw new UnauthorizedAccessException("ID de usuario inválido.");
        }

        return teacherId;
    }

    // =================== MÉTODOS ÚNICOS PARA ORIENTATIONREPORT ===================

    [HttpGet]
    public async Task<JsonResult> StudentsByGroupAndGrade(Guid groupId, Guid gradeId, Guid? subjectId = null)
    {
        try
        {
            IEnumerable<StudentBasicDto> students;
            if (subjectId.HasValue && subjectId.Value != Guid.Empty)
            {
                // Filtrar por materia, grupo y grado
                students = await _studentService.GetBySubjectGroupAndGradeAsync(subjectId.Value, groupId, gradeId);
            }
            else
            {
                // Filtrar solo por grupo y grado (comportamiento anterior)
                students = await _studentService.GetByGroupAndGradeAsync(groupId, gradeId);
            }
            return Json(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estudiantes por grupo y grado");
            return Json(new { error = "Error al obtener estudiantes" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GetAsistencias([FromBody] GetNotesDto request)
    {
        try
        {
            var teacherId = GetTeacherId();
            
            // Obtener asistencias para el grupo específico (usar fecha UTC para consistencia)
            var fechaActual = DateOnly.FromDateTime(DateTime.UtcNow);
            var asistencias = await _attendanceService.GetAttendancesByDateAsync(request.GroupId, request.GradeLevelId, fechaActual);
            
            return Json(new { success = true, data = asistencias });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener asistencias");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetByCounselor(string trimester = null)
    {
        try
        {
            var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
            if (!currentUserId.HasValue)
            {
                return Unauthorized(new { error = "Usuario no autenticado" });
            }

            var reports = await _orientationReportService.GetByCounselorAsync(currentUserId.Value, trimester);
            
            return Json(reports.Select(r => new {
                id = r.Id,
                studentName = r.StudentName,
                studentId = r.StudentId,
                date = r.Date.ToString("dd/MM/yyyy"),
                time = r.Date.ToString("HH:mm"),
                type = r.Type,
                category = r.Category,
                status = r.Status,
                description = r.Description,
                documents = r.Documents,
                teacher = r.Teacher,
                subjectName = r.SubjectName
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener reportes de orientación para consejero");
            return BadRequest(new { error = "Error al obtener los reportes de orientación" });
        }
    }
}
