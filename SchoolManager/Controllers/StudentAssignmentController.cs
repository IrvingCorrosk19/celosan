using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Application.Interfaces;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;

namespace SchoolManager.Controllers
{
    [Authorize(Roles = "admin,secretaria")]
    public class StudentAssignmentController : Controller
    {
        private readonly IUserService _userService;
        private readonly ISubjectService _subjectService;
        private readonly IGroupService _groupService;
        private readonly IGradeLevelService _gradeLevelService;
        private readonly IStudentAssignmentService _studentAssignmentService;
        private readonly ISubjectAssignmentService _subjectAssignmentService;
        private readonly IDateTimeHomologationService _dateTimeHomologationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IShiftService _shiftService;

        public StudentAssignmentController(
            IUserService userService,
            ISubjectService subjectService,
            IGroupService groupService,
            IGradeLevelService gradeLevelService,
            IStudentAssignmentService studentAssignmentService,
            ISubjectAssignmentService subjectAssignmentService,
            IDateTimeHomologationService dateTimeHomologationService,
            ICurrentUserService currentUserService,
            IShiftService shiftService)
        {
            _userService = userService;
            _subjectService = subjectService;
            _groupService = groupService;
            _gradeLevelService = gradeLevelService;
            _studentAssignmentService = studentAssignmentService;
            _subjectAssignmentService = subjectAssignmentService;
            _dateTimeHomologationService = dateTimeHomologationService;
            _currentUserService = currentUserService;
            _shiftService = shiftService;
        }

        [HttpPost("/StudentAssignment/UpdateGroupAndGrade")]
        public async Task<IActionResult> UpdateGroupAndGrade(Guid studentId, Guid gradeId, Guid groupId, bool additive = false)
        {
            if (studentId == Guid.Empty || gradeId == Guid.Empty || groupId == Guid.Empty)
                return Json(new { success = false, message = "Datos inválidos para la asignación." });

            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null)
                return Json(new { success = false, message = "Sesión no válida." });

            var student = await _userService.GetByIdAsync(studentId);
            if (student == null)
                return Json(new { success = false, message = "Estudiante no encontrado." });

            // Misma escuela que el usuario que edita (multi-tenant)
            if (currentUser.SchoolId.HasValue && student.SchoolId.HasValue &&
                currentUser.SchoolId != student.SchoolId)
                return Json(new { success = false, message = "No puede modificar estudiantes de otra institución." });

            var group = await _groupService.GetByIdAsync(groupId);
            if (group == null)
                return Json(new { success = false, message = "Grupo no válido." });

            if (!additive)
            {
                await _studentAssignmentService.RemoveAssignmentsAsync(studentId);
            }
            else
            {
                var already = await _studentAssignmentService.ExistsWithShiftAsync(studentId, gradeId, groupId, group.ShiftId);
                if (already)
                    return Json(new { success = true, message = "El estudiante ya tiene matrícula activa en ese grado, grupo y jornada." });
            }

            var newAssignment = new StudentAssignment
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                GradeId = gradeId,
                GroupId = groupId,
                ShiftId = group.ShiftId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                EnrollmentType = additive ? "Nocturno" : "Regular",
                StartDate = DateTime.UtcNow
            };
            await _studentAssignmentService.InsertAsync(newAssignment);

            return Json(new { success = true, message = additive ? "Matrícula adicional agregada." : "Asignación actualizada correctamente." });
        }

        /// <summary>Agrega una matrícula activa sin quitar las demás (p. ej. estudiante nocturno en dos grupos).</summary>
        [HttpPost("/StudentAssignment/AddEnrollment")]
        public async Task<IActionResult> AddEnrollment(Guid studentId, Guid gradeId, Guid groupId, string? enrollmentType = null)
        {
            if (studentId == Guid.Empty || gradeId == Guid.Empty || groupId == Guid.Empty)
                return Json(new { success = false, message = "Datos inválidos." });

            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null)
                return Json(new { success = false, message = "Sesión no válida." });

            var student = await _userService.GetByIdAsync(studentId);
            if (student == null)
                return Json(new { success = false, message = "Estudiante no encontrado." });

            if (currentUser.SchoolId.HasValue && student.SchoolId.HasValue &&
                currentUser.SchoolId != student.SchoolId)
                return Json(new { success = false, message = "No puede modificar estudiantes de otra institución." });

            var type = string.IsNullOrWhiteSpace(enrollmentType) ? "Nocturno" : enrollmentType.Trim();
            var added = await _studentAssignmentService.AddEnrollmentAsync(studentId, gradeId, groupId, type);
            if (!added)
                return Json(new { success = false, message = "Ya existe una matrícula activa para ese grado, grupo y jornada del grupo." });

            return Json(new { success = true, message = "Matrícula agregada correctamente." });
        }

        [HttpGet("/StudentAssignment/GetAvailableGradeGroups")]
        public async Task<IActionResult> GetAvailableGradeGroups()
        {
            var combinations = await _subjectAssignmentService.GetDistinctGradeGroupCombinationsAsync();

            var allGrades = await _gradeLevelService.GetAllAsync();
            var allGroups = await _groupService.GetAllAsync();

            var result = combinations.Select(c => 
            {
                var grade = allGrades.FirstOrDefault(g => g.Id == c.GradeLevelId);
                var group = allGroups.FirstOrDefault(g => g.Id == c.GroupId);
                var shift = !string.IsNullOrEmpty(group?.Shift) ? group.Shift : "Sin jornada";
                return new
                {
                    gradeId = c.GradeLevelId,
                    groupId = c.GroupId,
                    display = $"{grade?.Name ?? "-"} - {group?.Name ?? "-"} ({shift})"
                };
            }).OrderBy(x => x.display).ToList();

            return Json(new { success = true, data = result });
        }

        [HttpGet("/StudentAssignment/GetGradeGroupByStudent/{studentId}")]
        public async Task<IActionResult> GetGradeGroupByStudent(Guid studentId)
        {
            if (studentId == Guid.Empty)
                return Json(new { success = false, message = "ID de estudiante inválido." });

            var assignments = await _studentAssignmentService.GetAssignmentsByStudentIdAsync(studentId);

            if (assignments == null || !assignments.Any())
                return Json(new { success = true, data = Array.Empty<object>(), empty = true });

            var results = new List<(string grado, string grupo)>();
            foreach (var a in assignments)
            {
                var grade = await _gradeLevelService.GetByIdAsync(a.GradeId);
                var group = await _groupService.GetByIdAsync(a.GroupId);
                var shift = !string.IsNullOrEmpty(group?.Shift) ? group.Shift : "Sin jornada";
                results.Add((grade?.Name ?? "(Sin grado)", $"{group?.Name ?? "(Sin grupo)"} ({shift})"));
            }

            var distinct = results.Distinct().Select(x => new { grado = x.grado, grupo = x.grupo }).ToList();
            return Json(new { success = true, data = distinct });
        }

        [HttpGet]
        public async Task<IActionResult> GetAssignmentsByStudent(Guid id)
        {
            var student = await _userService.GetByIdAsync(id);
            if (student == null)
                return NotFound();

            var studentAssignments = await _studentAssignmentService.GetAssignmentsByStudentIdAsync(id);

            var subjectAssignments = new List<SubjectAssignment>();

            foreach (var sa in studentAssignments)
            {
                var matches = await _subjectService.GetSubjectAssignmentsByGradeAndGroupAsync(sa.GradeId, sa.GroupId);
                subjectAssignments.AddRange(matches);
            }

            var response = subjectAssignments.Select(a => new
            {
                materia = a.Subject?.Name ?? "(Sin materia)",
                grado = a.GradeLevel?.Name ?? "?",
                grupo = a.Group?.Name ?? "?",
                area = a.Area?.Name ?? "-",
                especialidad = a.Specialty?.Name ?? "-"
            }).Distinct();

            return Json(response);
        }

        public async Task<IActionResult> Index()
        {
            var students = await _userService.GetAllStudentsAsync();
            var allGroups = await _groupService.GetAllAsync();
            var allGrades = await _gradeLevelService.GetAllAsync();
            var allShifts = await _shiftService.GetAllAsync(); // Obtener jornadas del catálogo

            var assignmentsByStudent =
                await _studentAssignmentService.GetActiveAssignmentsForCurrentSchoolAsync();

            var gradeById = allGrades.ToDictionary(g => g.Id);
            var groupById = allGroups.ToDictionary(g => g.Id);
            var shiftById = allShifts.ToDictionary(s => s.Id);

            var viewModelList = new List<StudentAssignmentOverviewViewModel>();

            foreach (var student in students)
            {
                assignmentsByStudent.TryGetValue(student.Id, out var assignments);
                assignments ??= new List<StudentAssignment>();

                var gradeGroupPairs = assignments
                    .Select(a =>
                    {
                        var gradeName = gradeById.TryGetValue(a.GradeId, out var gr) ? gr.Name : "?";
                        groupById.TryGetValue(a.GroupId, out var group);
                        var groupName = group?.Name ?? "?";

                        string shiftName;
                        if (a.ShiftId.HasValue && shiftById.TryGetValue(a.ShiftId.Value, out var shDirect))
                            shiftName = shDirect.Name ?? "Sin jornada";
                        else if (group?.ShiftId != null && shiftById.TryGetValue(group.ShiftId.Value, out var shGroup))
                            shiftName = shGroup.Name ?? "Sin jornada";
                        else if (!string.IsNullOrEmpty(group?.Shift))
                            shiftName = group.Shift;
                        else
                            shiftName = "Sin jornada";

                        var tipo = string.IsNullOrWhiteSpace(a.EnrollmentType) ? "Regular" : a.EnrollmentType;
                        // Formato: Grado - Grupo | Jornada | Tipo de matrícula
                        return $"{gradeName} - {groupName} | Jornada: {shiftName} | Matrícula: {tipo}";
                    })
                    .Distinct()
                    .ToList();

                viewModelList.Add(new StudentAssignmentOverviewViewModel
                {
                    StudentId = student.Id,
                    FullName = student.Name,
                    FirstName = student.Name,
                    LastName = student.LastName ?? "",
                    DocumentId = student.DocumentId ?? "",
                    Email = student.Email,
                    IsActive = string.Equals(student.Status, "active", StringComparison.OrdinalIgnoreCase),
                    GradeGroupPairs = gradeGroupPairs
                });
            }

            return View(viewModelList);
        }

        public IActionResult Upload()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Assign(Guid id)
        {
            var student = await _userService.GetByIdAsync(id);
            if (student == null || student.Role?.ToLower() != "estudiante")
                return NotFound();

            var existingAssignments = await _studentAssignmentService.GetAssignmentsByStudentIdAsync(id);

            var model = new StudentAssignmentViewModel
            {
                StudentId = student.Id,
                SelectedGrades = existingAssignments.Select(x => x.GradeId).Distinct().ToList(),
                SelectedGroups = existingAssignments.Select(x => x.GroupId).Distinct().ToList(),
                AllSubjects = await _subjectService.GetAllAsync(),
                AllGrades = (await _gradeLevelService.GetAllAsync()).ToList(),
                AllGroups = await _groupService.GetAllAsync()
            };

            return View("Assign", model);
        }

        [HttpPost]
        public async Task<IActionResult> GuardarAsignacion([FromBody] StudentAssignmentRequest request)
        {
            if (request.GroupIds == null || !request.GroupIds.Any())
            {
                return BadRequest(new { success = false, message = "Debe seleccionar al menos un grupo." });
            }
              
            var insertedGroupIds = new List<Guid>();

            foreach (var groupId in request.GroupIds)
            {
                var inserted = await _studentAssignmentService.AssignStudentAsync(
                    request.UserId,
                    request.SubjectId,
                    request.GradeId,
                    groupId
                );

                if (inserted)
                {
                    insertedGroupIds.Add(groupId);
                }
            }

            if (!insertedGroupIds.Any())
            {
                return Ok(new
                {
                    success = false,
                    message = "Estas combinaciones ya existen. No se guardaron nuevas asignaciones."
                });
            }

            var subject = await _subjectService.GetByIdAsync(request.SubjectId);
            var grade = await _gradeLevelService.GetByIdAsync(request.GradeId);
            var allGroups = await _groupService.GetAllAsync();

            var insertedGroupNames = allGroups
                .Where(g => insertedGroupIds.Contains(g.Id))
                .Select(g => g.Name)
                .ToList();

            return Ok(new
            {
                request.UserId,
                request.SubjectId,
                SubjectName = subject?.Name,
                request.GradeId,
                GradeName = grade?.Name,
                GroupIds = insertedGroupIds,
                GroupNames = insertedGroupNames,
                success = true,
                message = "Asignación guardada correctamente."
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAssignments(Guid userId, List<Guid> subjectIds, List<Guid> groupIds, List<Guid> gradeLevelIds)
        {
            var user = await _userService.GetByIdWithRelationsAsync(userId);
            if (user == null) return NotFound();

            await _userService.UpdateAsync(user, subjectIds, groupIds, gradeLevelIds);

            return Json(new { success = true, message = "Asignaciones actualizadas correctamente." });
        }
        [HttpPost]
        private async Task<Guid?> GetCurrentUserSchoolId()
        {
            try
            {
                // Obtener el usuario actual desde el contexto de autenticación
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    Console.WriteLine("[GetCurrentUserSchoolId] No se pudo obtener el email del usuario actual");
                    return null;
                }

                var currentUser = await _userService.GetByEmailAsync(userEmail);
                return currentUser?.SchoolId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCurrentUserSchoolId] Error: {ex.Message}");
                return null;
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveAssignments([FromBody] List<StudentAssignmentInputModel> asignaciones)
        {
            if (asignaciones == null || asignaciones.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron asignaciones." });

            int insertadas = 0;
            int duplicadas = 0;
            int estudiantesCreados = 0;
            var errores = new List<string>();

            foreach (var item in asignaciones)
            {
                try
                {
                    Console.WriteLine($"[SaveAssignments] Procesando: {item.Estudiante} - {item.Grado} - {item.Grupo}");
                    
                    // Buscar o crear el estudiante
                    var student = await _userService.GetByEmailAsync(item.Estudiante);
                    if (student == null)
                    {
                        Console.WriteLine($"[SaveAssignments] Estudiante no encontrado, creando: {item.Estudiante}");
                        
                        // Crear el estudiante automáticamente
                        var newStudent = new User
                        {
                            Id = Guid.NewGuid(),
                            Email = item.Estudiante,
                            Name = !string.IsNullOrEmpty(item.Nombre) ? item.Nombre : item.Estudiante.Split('@')[0],
                            LastName = !string.IsNullOrEmpty(item.Apellido) ? item.Apellido : "Estudiante",
                            DocumentId = !string.IsNullOrEmpty(item.DocumentoId) ? item.DocumentoId : $"EST-{Guid.NewGuid().ToString("N")[..8]}",
                            DateOfBirth = _dateTimeHomologationService.HomologateDateOfBirth(
                                item.FechaNacimiento, 
                                "StudentAssignment"
                            ),
                            Role = "estudiante",
                            Status = "active",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            SchoolId = await GetCurrentUserSchoolId(),
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), // Contraseña temporal por defecto hasheada
                            TwoFactorEnabled = false,
                            LastLogin = null,
                            Inclusivo = item.Inclusivo,
                            Shift = !string.IsNullOrEmpty(item.Jornada) ? item.Jornada.Trim() : null // Jornada del estudiante
                        };
                        
                        await _userService.CreateAsync(newStudent, new List<Guid>(), new List<Guid>());
                        student = newStudent;
                        estudiantesCreados++;
                        
                        Console.WriteLine($"[SaveAssignments] Estudiante creado con ID: {student.Id}, Jornada: {student.Shift}");
                    }
                    else
                    {
                        Console.WriteLine($"[SaveAssignments] Estudiante encontrado, actualizando campos Inclusivo y Jornada: {item.Estudiante}");
                        
                        // Actualizar el campo Inclusivo y Jornada del estudiante existente
                        student.Inclusivo = item.Inclusivo;
                        if (!string.IsNullOrEmpty(item.Jornada))
                        {
                            student.Shift = item.Jornada.Trim();
                        }
                        student.UpdatedAt = DateTime.UtcNow;
                        
                        await _userService.UpdateAsync(student, new List<Guid>(), new List<Guid>());
                        
                        Console.WriteLine($"[SaveAssignments] Campos Inclusivo y Jornada actualizados para estudiante: {student.Id}, Jornada: {student.Shift}");
                    }

                    var grade = await _gradeLevelService.GetByNameAsync(item.Grado);
                    var group = await _groupService.GetByNameAndGradeAsync(item.Grupo);
                    
                    // Buscar o crear jornada si se proporcionó (similar a grado y grupo)
                    Shift? shift = null;
                    if (!string.IsNullOrEmpty(item.Jornada))
                    {
                        var jornadaNombre = item.Jornada.Trim();
                        shift = await _shiftService.GetOrCreateAsync(jornadaNombre);
                        
                        // Si el grupo existe y no tiene jornada, asignarla al grupo
                        if (group != null && (group.ShiftId == null || group.ShiftId != shift.Id))
                        {
                            group.ShiftId = shift.Id;
                            group.Shift = shift.Name; // Mantener por compatibilidad
                            group.UpdatedAt = DateTime.UtcNow;
                            await _groupService.UpdateAsync(group);
                            Console.WriteLine($"[SaveAssignments] Jornada '{shift.Name}' (ID: {shift.Id}) asignada al grupo {group.Name}");
                        }
                    }

                    if (grade == null || group == null)
                    {
                        errores.Add($"Error de datos: {item.Estudiante} - {item.Grado} - {item.Grupo} (Grado o Grupo no encontrado)");
                        continue;
                    }

                    Console.WriteLine($"[SaveAssignments] Verificando si existe asignación: StudentId={student.Id}, GradeId={grade.Id}, GroupId={group.Id}, ShiftId={shift?.Id}");
                    
                    bool exists = await _studentAssignmentService.ExistsWithShiftAsync(student.Id, grade.Id, group.Id, shift?.Id);
                    if (exists)
                    {
                        Console.WriteLine($"[SaveAssignments] Asignación ya existe, saltando");
                        duplicadas++;
                        continue;
                    }

                    var tipoMatricula = string.IsNullOrWhiteSpace(item.TipoMatricula) ? "Regular" : item.TipoMatricula.Trim();
                    var assignment = new StudentAssignment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student.Id,
                        GradeId = grade.Id,
                        GroupId = group.Id,
                        ShiftId = shift?.Id, // Asignar jornada directamente a la asignación (similar a grado y grupo)
                        CreatedAt = DateTime.UtcNow,
                        EnrollmentType = tipoMatricula,
                        StartDate = DateTime.UtcNow
                    };

                    Console.WriteLine($"[SaveAssignments] Creando nueva asignación");
                    await _studentAssignmentService.InsertAsync(assignment);
                    insertadas++;
                    Console.WriteLine($"[SaveAssignments] Asignación creada exitosamente");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SaveAssignments] Excepción en {item.Estudiante}: {ex.Message}");
                    Console.WriteLine($"[SaveAssignments] StackTrace: {ex.StackTrace}");
                    errores.Add($"Excepción en {item.Estudiante}: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = true,
                insertadas,
                duplicadas,
                estudiantesCreados,
                errores,
                message = "Carga masiva completada."
            });
        }


    }
}
