using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Interfaces;
using SchoolManager.Scripts;

namespace SchoolManager.Controllers;

[Authorize]
public class PrematriculationController : Controller
{
    private readonly IPrematriculationService _prematriculationService;
    private readonly IPrematriculationPeriodService _periodService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStudentService _studentService;
    private readonly IGradeLevelService _gradeLevelService;
    private readonly ICelosamPrematriculationModuleService _celosamPrematriculationModuleService;
    private readonly SchoolDbContext _context;
    private readonly ILogger<PrematriculationController> _logger;

    public PrematriculationController(
        IPrematriculationService prematriculationService,
        IPrematriculationPeriodService periodService,
        ICurrentUserService currentUserService,
        IStudentService studentService,
        IGradeLevelService gradeLevelService,
        ICelosamPrematriculationModuleService celosamPrematriculationModuleService,
        SchoolDbContext context,
        ILogger<PrematriculationController> logger)
    {
        _prematriculationService = prematriculationService;
        _periodService = periodService;
        _currentUserService = currentUserService;
        _studentService = studentService;
        _gradeLevelService = gradeLevelService;
        _celosamPrematriculationModuleService = celosamPrematriculationModuleService;
        _context = context;
        _logger = logger;
    }

    // Vista para estudiantes/acudientes: ver prematrículas del estudiante
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> MyPrematriculations()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        // Si el usuario es estudiante, buscar prematrículas donde él es el estudiante
        // Si es acudiente, buscar prematrículas donde él es el padre
        var userRole = currentUser.Role?.ToLower() ?? "";
        List<PrematriculationDto> prematriculations;
        
        if (userRole == "student" || userRole == "estudiante")
        {
            // El usuario actual es estudiante, buscar sus propias prematrículas
            prematriculations = await _prematriculationService.GetByStudentAsync(currentUser.Id);
        }
        else
        {
            // El usuario es acudiente/padre, buscar prematrículas de sus hijos
            prematriculations = await _prematriculationService.GetByParentAsync(currentUser.Id);
        }
        
        return View(prematriculations);
    }

    // Vista para estudiantes/acudientes: crear nueva prematrícula
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> Create()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        // Verificar si hay un período activo
        var activePeriod = await _periodService.GetActivePeriodAsync(currentUser.SchoolId.Value);
        if (activePeriod == null)
        {
            TempData["ErrorMessage"] = "El período de prematrícula no está disponible";
            return RedirectToAction(nameof(MyPrematriculations));
        }

        ViewBag.ActivePeriod = activePeriod;
        
        var userRole = currentUser.Role?.ToLower() ?? "";
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.IsStudentUser = (userRole == "student" || userRole == "estudiante");
        
        // Si el usuario es estudiante, solo puede prematricularse a sí mismo
        if (userRole == "student" || userRole == "estudiante")
        {
            ViewBag.Students = new[] { new { Id = currentUser.Id, Name = $"{currentUser.Name} {currentUser.LastName}" } };
        }
        else
        {
            // Si es acudiente, obtener estudiantes asociados (hijos)
            if (currentUserId.HasValue && currentUser.SchoolId.HasValue)
            {
                // Obtener estudiantes de la tabla Students que tengan este usuario como padre
                var studentsFromStudentsTable = await _context.Students
                    .Where(s => s.ParentId == currentUserId.Value)
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync();
                
                // También buscar en Users estudiantes de la misma escuela
                // (esto es un fallback si no están en la tabla Students)
                var studentsFromUsers = await _context.Users
                    .Where(u => (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante")
                        && u.SchoolId == currentUser.SchoolId.Value)
                    .Select(u => new { Id = u.Id, Name = $"{u.Name} {u.LastName}" })
                    .ToListAsync();
                
                // Si hay estudiantes en la tabla Students con ParentId, usarlos
                // Si no, usar todos los estudiantes de la escuela (mejorar con relación Parent-Student)
                ViewBag.Students = studentsFromStudentsTable.Any() ? studentsFromStudentsTable : studentsFromUsers;
            }
        }
        
        // Obtener grados disponibles
        var allGrades = await _gradeLevelService.GetAllAsync();
        
        // Si hay un estudiante seleccionado (o es estudiante), filtrar grados disponibles
        List<GradeLevel> availableGrades = new List<GradeLevel>();
        
        // Función helper para extraer número del grado (ej: "5°" -> 5)
        int? ExtractGradeNumber(string gradeName)
        {
            if (string.IsNullOrEmpty(gradeName))
                return null;
            
            // Extraer número del nombre del grado (ej: "5°", "10°", "11°")
            var match = System.Text.RegularExpressions.Regex.Match(gradeName, @"(\d+)");
            if (match.Success && int.TryParse(match.Value, out int gradeNum))
                return gradeNum;
            
            return null;
        }
        
        // Si es estudiante, obtener su grado actual
        if (userRole == "student" || userRole == "estudiante")
        {
            var currentStudentId = currentUser.Id;
            var currentGrade = await _context.StudentAssignments
                .Where(sa => sa.StudentId == currentStudentId)
                .OrderByDescending(sa => sa.CreatedAt)
                .Include(sa => sa.Grade)
                .Select(sa => sa.Grade)
                .FirstOrDefaultAsync();
            
            if (currentGrade != null)
            {
                var currentGradeNum = ExtractGradeNumber(currentGrade.Name);
                if (currentGradeNum.HasValue)
                {
                    // Permitir solo el siguiente nivel (o el mismo si reprueba)
                    // Por ejemplo: si está en 5°, solo puede elegir 6° o 5° (repetir)
                    var allowedGrades = allGrades.Where(g => 
                    {
                        var gradeNum = ExtractGradeNumber(g.Name);
                        return gradeNum.HasValue && 
                               (gradeNum.Value == currentGradeNum.Value || // Repetir
                                gradeNum.Value == currentGradeNum.Value + 1); // Siguiente
                    }).ToList();
                    
                    availableGrades = allowedGrades;
                }
                else
                {
                    // Si no se puede extraer el número, mostrar todos los grados
                    availableGrades = allGrades.ToList();
                }
            }
            else
            {
                // Si no tiene grado actual, mostrar todos los grados (estudiante nuevo)
                availableGrades = allGrades.ToList();
            }
        }
        else
        {
            // Para acudientes, mostrar todos los grados (se validará cuando seleccionen estudiante)
            availableGrades = allGrades.ToList();
        }
        
        ViewBag.Grades = availableGrades;
        
        // Proyectar a objetos simples para evitar ciclos de referencia en JSON
        ViewBag.AllGrades = allGrades.Select(g => new { id = g.Id, name = g.Name }).ToList();

        // Logs de depuración
        Console.WriteLine($"[DEBUG] Prematriculation/Create - Total grados: {allGrades.Count()}");
        Console.WriteLine($"[DEBUG] Prematriculation/Create - Grados disponibles: {availableGrades.Count()}");
        Console.WriteLine($"[DEBUG] Prematriculation/Create - ViewBag.Grades es null: {ViewBag.Grades == null}");
        if (availableGrades != null && availableGrades.Any())
        {
            Console.WriteLine($"[DEBUG] Prematriculation/Create - Primer grado disponible: {availableGrades.First().Name} (ID: {availableGrades.First().Id})");
        }
        else
        {
            Console.WriteLine("[DEBUG] Prematriculation/Create - ¡NO HAY GRADOS DISPONIBLES!");
            _logger.LogWarning("Prematriculation/Create - No hay grados disponibles para mostrar. Total grados en sistema: {TotalGrades}, Grados disponibles filtrados: {AvailableGrades}", 
                allGrades.Count(), availableGrades?.Count() ?? 0);
        }

        return View();
    }

    [HttpPost]
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> Create(PrematriculationCreateDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        try
        {
            // Validar que el período esté activo
            var activePeriod = await _periodService.GetActivePeriodAsync(currentUser.SchoolId!.Value);
            if (activePeriod == null)
            {
                ModelState.AddModelError("", "El período de prematrícula no está disponible");
                return View(dto);
            }

            dto.PrematriculationPeriodId = activePeriod.Id;

            // Validar condición académica
            var isValid = await _prematriculationService.ValidateAcademicConditionAsync(dto.StudentId);
            if (!isValid)
            {
                var failedCount = await _prematriculationService.GetFailedSubjectsCountAsync(dto.StudentId);
                ModelState.AddModelError("", 
                    $"El estudiante no puede participar en la prematrícula por exceder el límite de materias reprobadas ({failedCount} materias reprobadas)");
                return View(dto);
            }

            // Validar que el grado seleccionado sea válido para el estudiante
            if (dto.GradeId.HasValue)
            {
                var studentCurrentGrade = await _context.StudentAssignments
                    .Where(sa => sa.StudentId == dto.StudentId)
                    .OrderByDescending(sa => sa.CreatedAt)
                    .Include(sa => sa.Grade)
                    .Select(sa => sa.Grade)
                    .FirstOrDefaultAsync();

                if (studentCurrentGrade != null)
                {
                    var selectedGrade = await _gradeLevelService.GetByIdAsync(dto.GradeId.Value);
                    if (selectedGrade != null)
                    {
                        // Función helper para extraer número del grado
                        int? ExtractGradeNumber(string gradeName)
                        {
                            if (string.IsNullOrEmpty(gradeName))
                                return null;
                            var match = System.Text.RegularExpressions.Regex.Match(gradeName, @"(\d+)");
                            if (match.Success && int.TryParse(match.Value, out int gradeNum))
                                return gradeNum;
                            return null;
                        }

                        var currentGradeNum = ExtractGradeNumber(studentCurrentGrade.Name);
                        var selectedGradeNum = ExtractGradeNumber(selectedGrade.Name);

                        if (currentGradeNum.HasValue && selectedGradeNum.HasValue)
                        {
                            // Solo puede elegir el siguiente nivel o el mismo (repetir)
                            if (selectedGradeNum.Value < currentGradeNum.Value)
                            {
                                ModelState.AddModelError("GradeId", 
                                    $"El estudiante no puede retroceder de nivel. Actualmente está en {studentCurrentGrade.Name} y solo puede prematricularse para el siguiente nivel o repetir el mismo.");
                                // Recargar datos necesarios para la vista
                                var allGrades = await _gradeLevelService.GetAllAsync();
                                ViewBag.Grades = allGrades;
                                // Proyectar a objetos simples para evitar ciclos de referencia en JSON
                                ViewBag.AllGrades = allGrades.Select(g => new { id = g.Id, name = g.Name }).ToList();
                                return View(dto);
                            }
                            else if (selectedGradeNum.Value > currentGradeNum.Value + 1)
                            {
                                ModelState.AddModelError("GradeId", 
                                    $"El estudiante no puede saltar niveles. Actualmente está en {studentCurrentGrade.Name} y solo puede prematricularse para el siguiente nivel o repetir el mismo.");
                                // Recargar datos necesarios para la vista
                                var allGrades = await _gradeLevelService.GetAllAsync();
                                ViewBag.Grades = allGrades;
                                // Proyectar a objetos simples para evitar ciclos de referencia en JSON
                                ViewBag.AllGrades = allGrades.Select(g => new { id = g.Id, name = g.Name }).ToList();
                                return View(dto);
                            }
                        }
                    }
                }
            }

            // Si el usuario es estudiante y se prematricula a sí mismo, ParentId será null
            // Si es acudiente prematriculando a su hijo, usar currentUser.Id como parentId
            var userRole = currentUser.Role?.ToLower() ?? "";
            Guid? parentId = null;
            
            // Si el estudiante se prematricula a sí mismo (mismo ID), no hay parentId
            if (dto.StudentId == currentUser.Id)
            {
                // El estudiante se prematricula a sí mismo, ParentId es null
                parentId = null;
            }
            else if (userRole == "parent" || userRole == "acudiente")
            {
                // Es acudiente prematriculando a su hijo
                parentId = currentUser.Id;
            }
            
            var prematriculation = await _prematriculationService.CreatePrematriculationAsync(dto, parentId);
            
            TempData["SuccessMessage"] = $"Prematrícula creada exitosamente. Código: {prematriculation.PrematriculationCode}";
            return RedirectToAction("PayWithCard", "Payment", new { prematriculationId = prematriculation.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear prematrícula");
            ModelState.AddModelError("", "Error al crear la prematrícula: " + ex.Message);
            return View(dto);
        }
    }

    // Obtener grados disponibles para un estudiante (AJAX)
    [HttpGet]
    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    public async Task<IActionResult> GetAvailableGrades(Guid? studentId)
    {
        if (!studentId.HasValue)
            return Json(new List<object>());

        var allGrades = await _gradeLevelService.GetAllAsync();
        var availableGrades = new List<object>();

        // Función helper para extraer número del grado
        int? ExtractGradeNumber(string gradeName)
        {
            if (string.IsNullOrEmpty(gradeName))
                return null;
            var match = System.Text.RegularExpressions.Regex.Match(gradeName, @"(\d+)");
            if (match.Success && int.TryParse(match.Value, out int gradeNum))
                return gradeNum;
            return null;
        }

        // Obtener grado actual del estudiante
        var currentGrade = await _context.StudentAssignments
            .Where(sa => sa.StudentId == studentId.Value)
            .OrderByDescending(sa => sa.CreatedAt)
            .Include(sa => sa.Grade)
            .Select(sa => sa.Grade)
            .FirstOrDefaultAsync();

        if (currentGrade != null)
        {
            var currentGradeNum = ExtractGradeNumber(currentGrade.Name);
            if (currentGradeNum.HasValue)
            {
                // Permitir solo el siguiente nivel (o el mismo si reprueba)
                availableGrades = allGrades
                    .Where(g =>
                    {
                        var gradeNum = ExtractGradeNumber(g.Name);
                        return gradeNum.HasValue &&
                               (gradeNum.Value == currentGradeNum.Value || // Repetir
                                gradeNum.Value == currentGradeNum.Value + 1); // Siguiente
                    })
                    .Select(g => new { id = g.Id, name = g.Name })
                    .Cast<object>()
                    .ToList();
            }
            else
            {
                // Si no se puede extraer el número, mostrar todos los grados
                availableGrades = allGrades
                    .Select(g => new { id = g.Id, name = g.Name })
                    .Cast<object>()
                    .ToList();
            }
        }
        else
        {
            // Si no tiene grado actual, mostrar todos los grados (estudiante nuevo)
            availableGrades = allGrades
                .Select(g => new { id = g.Id, name = g.Name })
                .Cast<object>()
                .ToList();
        }

        return Json(availableGrades);
    }

    // Obtener grupos disponibles para un grado (AJAX)
    [HttpGet]
    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    public async Task<IActionResult> GetAvailableGroups(Guid? gradeId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var groups = await _prematriculationService.GetAvailableGroupsAsync(currentUser.SchoolId.Value, gradeId);
        return Json(groups);
    }

    // Vista para administradores: ver todas las prematrículas
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var activePeriod = await _periodService.GetActivePeriodAsync(currentUser.SchoolId.Value);
        if (activePeriod == null)
        {
            TempData["InfoMessage"] = "No hay un período de prematrícula activo";
            return View(new List<PrematriculationDto>());
        }

        var prematriculations = await _prematriculationService.GetByPeriodAsync(activePeriod.Id);
        return View(prematriculations);
    }

    // Vista para administradores: confirmar matrícula
    [HttpPost]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> ConfirmMatriculation(Guid id)
    {
        try
        {
            await _prematriculationService.ConfirmMatriculationAsync(id);
            TempData["SuccessMessage"] = "Matrícula confirmada exitosamente";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al confirmar matrícula");
            TempData["ErrorMessage"] = "Error al confirmar la matrícula: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // Vista para docentes: seleccionar grupo para ver prematrículas
    [Authorize(Roles = "teacher,docente")]
    public async Task<IActionResult> SelectGroup()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        // Obtener grupos asignados al docente a través de sus asignaciones académicas
        var groups = await _context.TeacherAssignments
            .Where(ta => ta.TeacherId == currentUser.Id)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Group)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.GradeLevel)
            .Select(ta => new
            {
                Group = ta.SubjectAssignment.Group,
                GradeLevel = ta.SubjectAssignment.GradeLevel
            })
            .Where(x => x.Group != null)
            .Distinct()
            .Select(x => new
            {
                x.Group.Id,
                GroupName = x.Group.Name,
                GradeName = x.GradeLevel != null ? x.GradeLevel.Name : (x.Group.Grade ?? "Sin grado"),
                DisplayName = (x.GradeLevel != null ? x.GradeLevel.Name : (x.Group.Grade ?? "Sin grado")) + " - " + x.Group.Name
            })
            .ToListAsync();

        ViewBag.Groups = groups;
        return View();
    }

    // Vista para docentes: ver estudiantes prematriculados/matriculados por grupo
    [Authorize(Roles = "teacher,docente,admin,superadmin")]
    public async Task<IActionResult> ByGroup(Guid groupId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var userRole = currentUser.Role?.ToLower() ?? "";
        
        // Si es docente, verificar que tenga acceso al grupo
        if (userRole == "teacher" || userRole == "docente")
        {
            var hasAccess = await _context.TeacherAssignments
                .Include(ta => ta.SubjectAssignment)
                .AnyAsync(ta => ta.TeacherId == currentUser.Id && 
                              ta.SubjectAssignment != null && 
                              ta.SubjectAssignment.GroupId == groupId);

            if (!hasAccess)
                return Forbid();
        }

        var group = await _context.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return NotFound();

        var prematriculations = await _prematriculationService.GetByGroupAsync(groupId);
        ViewBag.Group = group;
        return View(prematriculations);
    }

    // Comprobante de matrícula (PDF/Vista imprimible)
    [Authorize]
    public async Task<IActionResult> Certificate(Guid id)
    {
        var prematriculation = await _prematriculationService.GetByIdAsync(id);
        if (prematriculation == null)
            return NotFound();

        // Verificar que el usuario tenga acceso (acudiente, estudiante o admin)
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var userRole = currentUser.Role?.ToLower() ?? "";
        var isAuthorized = userRole == "admin" || userRole == "superadmin" ||
                          prematriculation.StudentId == currentUser.Id ||
                          prematriculation.ParentId == currentUser.Id;

        if (!isAuthorized)
            return Forbid();

        // Solo mostrar si está matriculado
        if (prematriculation.Status != "Matriculado")
        {
            TempData["ErrorMessage"] = "Solo se puede generar el comprobante de matrícula para estudiantes matriculados";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(prematriculation);
    }

    // Detalles de una prematrícula
    public async Task<IActionResult> Details(Guid id)
    {
        var prematriculation = await _prematriculationService.GetByIdAsync(id);
        if (prematriculation == null)
            return NotFound();

        var dto = new PrematriculationDto
        {
            Id = prematriculation.Id,
            StudentId = prematriculation.StudentId,
            StudentName = $"{prematriculation.Student.Name} {prematriculation.Student.LastName}",
            StudentDocumentId = prematriculation.Student.DocumentId ?? "",
            Status = prematriculation.Status,
            PrematriculationCode = prematriculation.PrematriculationCode,
            FailedSubjectsCount = prematriculation.FailedSubjectsCount,
            AcademicConditionValid = prematriculation.AcademicConditionValid,
            CreatedAt = prematriculation.CreatedAt,
            PaymentDate = prematriculation.PaymentDate,
            MatriculationDate = prematriculation.MatriculationDate
        };

        return View(dto);
    }

    // Endpoint temporal para aplicar cambios a la base de datos
    // TODO: Remover después de aplicar los cambios
    [Authorize(Roles = "admin,superadmin")]
    [HttpPost]
    [Route("/Prematriculation/ApplyDatabaseChanges")]
    public async Task<IActionResult> ApplyDatabaseChanges()
    {
        try
        {
            await SchoolManager.Scripts.ApplyDatabaseChanges.ApplyPrematriculationChangesAsync(_context);
            TempData["Success"] = "Cambios aplicados correctamente a la base de datos";
            return Json(new { success = true, message = "Cambios aplicados correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aplicar cambios a la base de datos");
            TempData["Error"] = $"Error al aplicar cambios: {ex.Message}";
            return Json(new { success = false, message = ex.Message });
        }
    }

    [Authorize(Roles = "admin,superadmin")]
    [HttpGet]
    [Route("/Prematriculation/ApplyDatabaseChanges")]
    public IActionResult ApplyDatabaseChangesPage()
    {
        return View("ApplyDatabaseChanges");
    }

    // Endpoint para aplicar cambios de Año Académico
    [Authorize(Roles = "admin,superadmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Prematriculation/ApplyAcademicYearChanges")]
    public async Task<IActionResult> ApplyAcademicYearChanges()
    {
        try
        {
            _logger.LogInformation("Aplicando cambios de Año Académico desde la interfaz web");
            await SchoolManager.Scripts.ApplyAcademicYearChanges.ApplyAsync(_context);
            TempData["Success"] = "Cambios de Año Académico aplicados correctamente a la base de datos";
            return Json(new { success = true, message = "Cambios de Año Académico aplicados correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aplicar cambios de Año Académico");
            TempData["Error"] = $"Error al aplicar cambios: {ex.Message}";
            return Json(new { success = false, message = ex.Message });
        }
    }

    [Authorize(Roles = "admin,superadmin")]
    [HttpGet]
    [Route("/Prematriculation/ApplyAcademicYearChanges")]
    public IActionResult ApplyAcademicYearChangesPage()
    {
        return View("ApplyAcademicYearChanges");
    }

    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    [HttpGet]
    public async Task<IActionResult> ModularSubjects(Guid id)
    {
        var model = await _celosamPrematriculationModuleService.GetDashboardAsync(id);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectModularSubject(Guid prematriculationId, Guid curriculumSubjectId)
    {
        var result = await _celosamPrematriculationModuleService.SelectSubjectAsync(prematriculationId, curriculumSubjectId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(ModularSubjects), new { id = prematriculationId });
    }

    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveModularSubject(Guid prematriculationId, Guid selectionId)
    {
        var result = await _celosamPrematriculationModuleService.RemoveSubjectAsync(selectionId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(ModularSubjects), new { id = prematriculationId });
    }

    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeModular(Guid id)
    {
        var result = await _celosamPrematriculationModuleService.FinalizeAsync(id);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        if (result.Success && result.ReceiptId.HasValue)
            return RedirectToAction(nameof(ModularReceiptPdf), new { receiptId = result.ReceiptId.Value });

        return RedirectToAction(nameof(ModularSubjects), new { id });
    }

    [Authorize(Roles = "admin,superadmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenModular(Guid id, string reason)
    {
        var result = await _celosamPrematriculationModuleService.ReopenAsync(id, reason);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(ModularSubjects), new { id });
    }

    [Authorize(Roles = "acudiente,parent,student,estudiante,admin,superadmin")]
    [HttpGet]
    public async Task<IActionResult> ModularReceiptPdf(Guid receiptId)
    {
        var pdf = await _celosamPrematriculationModuleService.GenerateReceiptPdfAsync(receiptId);
        if (pdf == null)
            return NotFound();

        return File(pdf, "application/pdf", $"comprobante-prematricula-{receiptId:N}.pdf");
    }
}

