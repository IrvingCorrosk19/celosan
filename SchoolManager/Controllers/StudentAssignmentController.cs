using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Application.Interfaces;
using SchoolManager.Models;
using SchoolManager.Services.Implementations;
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
        private readonly SchoolDbContext _context;

        public StudentAssignmentController(
            IUserService userService,
            ISubjectService subjectService,
            IGroupService groupService,
            IGradeLevelService gradeLevelService,
            IStudentAssignmentService studentAssignmentService,
            ISubjectAssignmentService subjectAssignmentService,
            IDateTimeHomologationService dateTimeHomologationService,
            ICurrentUserService currentUserService,
            IShiftService shiftService,
            SchoolDbContext context)
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
            _context = context;
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

            var subjectAssignments = await _subjectService.GetSubjectAssignmentsByStudentAsync(id);

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

        [HttpGet("/StudentAssignment/GetSubjectEnrollmentsByStudent/{studentId}")]
        public async Task<IActionResult> GetSubjectEnrollmentsByStudent(Guid studentId)
        {
            var data = await _context.StudentSubjectAssignments
                .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
                .Join(_context.SubjectAssignments,
                    ssa => ssa.SubjectAssignmentId,
                    sa => sa.Id,
                    (ssa, sa) => new { ssa, sa })
                .Join(_context.Subjects,
                    x => x.sa.SubjectId,
                    s => s.Id,
                    (x, s) => new { x.ssa, x.sa, subject = s })
                .Join(_context.GradeLevels,
                    x => x.sa.GradeLevelId,
                    g => g.Id,
                    (x, g) => new { x.ssa, x.sa, x.subject, grade = g })
                .Join(_context.Groups,
                    x => x.sa.GroupId,
                    g => g.Id,
                    (x, g) => new
                    {
                        enrollmentId = x.ssa.Id,
                        subjectAssignmentId = x.sa.Id,
                        subjectName = x.subject.Name,
                        gradeName = x.grade.Name,
                        groupName = g.Name,
                        status = x.ssa.Status,
                        enrollmentType = x.ssa.EnrollmentType
                    })
                .OrderBy(x => x.subjectName)
                .ThenBy(x => x.gradeName)
                .ThenBy(x => x.groupName)
                .ToListAsync();

            return Json(new { success = true, data });
        }

        [HttpGet("/StudentAssignment/GetAvailableSubjectCatalog")]
        public async Task<IActionResult> GetAvailableSubjectCatalog(Guid studentId)
        {
            var activeAssignments = await _studentAssignmentService.GetAssignmentsByStudentIdAsync(studentId);
            if (activeAssignments == null || !activeAssignments.Any())
                return Json(new { success = true, data = Array.Empty<object>() });

            var gradeIds = activeAssignments.Select(x => x.GradeId).Distinct().ToList();
            var groupIds = activeAssignments.Select(x => x.GroupId).Distinct().ToList();

            var currentEnrollmentIds = await _context.StudentSubjectAssignments
                .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
                .Select(ssa => ssa.SubjectAssignmentId)
                .ToListAsync();

            var catalog = await _context.SubjectAssignments
                .Where(sa => gradeIds.Contains(sa.GradeLevelId) || groupIds.Contains(sa.GroupId))
                .Where(sa => !currentEnrollmentIds.Contains(sa.Id))
                .Join(_context.Subjects, sa => sa.SubjectId, s => s.Id, (sa, s) => new { sa, subject = s })
                .Join(_context.GradeLevels, x => x.sa.GradeLevelId, g => g.Id, (x, g) => new { x.sa, x.subject, grade = g })
                .Join(_context.Groups, x => x.sa.GroupId, g => g.Id, (x, g) => new
                {
                    subjectAssignmentId = x.sa.Id,
                    subjectName = x.subject.Name,
                    gradeName = x.grade.Name,
                    groupName = g.Name,
                    display = $"{x.subject.Name} | {x.grade.Name} - {g.Name}"
                })
                .OrderBy(x => x.subjectName)
                .ThenBy(x => x.gradeName)
                .ThenBy(x => x.groupName)
                .ToListAsync();

            return Json(new { success = true, data = catalog });
        }

        [HttpPost("/StudentAssignment/AddSubjectEnrollment")]
        public async Task<IActionResult> AddSubjectEnrollment(Guid studentId, Guid subjectAssignmentId)
        {
            if (studentId == Guid.Empty || subjectAssignmentId == Guid.Empty)
                return Json(new { success = false, message = "Datos inválidos." });

            var subjectAssignment = await _context.SubjectAssignments.FirstOrDefaultAsync(sa => sa.Id == subjectAssignmentId);
            if (subjectAssignment == null)
                return Json(new { success = false, message = "La asignatura seleccionada no existe." });

            var baseAssignment = await _context.StudentAssignments
                .Where(sa => sa.StudentId == studentId && sa.IsActive &&
                             sa.GradeId == subjectAssignment.GradeLevelId &&
                             sa.GroupId == subjectAssignment.GroupId)
                .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
                .FirstOrDefaultAsync();

            if (baseAssignment == null)
            {
                baseAssignment = await _context.StudentAssignments
                    .Where(sa => sa.StudentId == studentId && sa.IsActive)
                    .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            if (baseAssignment == null)
                return Json(new { success = false, message = "El estudiante necesita al menos una matrícula activa antes de asignar materias." });

            var exists = await _context.StudentSubjectAssignments.AnyAsync(ssa =>
                ssa.StudentId == studentId &&
                ssa.SubjectAssignmentId == subjectAssignmentId &&
                ssa.IsActive &&
                ssa.AcademicYearId == baseAssignment.AcademicYearId);

            if (exists)
                return Json(new { success = false, message = "La materia ya está asignada al estudiante." });

            var enrollment = new StudentSubjectAssignment
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                SubjectAssignmentId = subjectAssignmentId,
                StudentAssignmentId = baseAssignment.Id,
                AcademicYearId = baseAssignment.AcademicYearId,
                ShiftId = baseAssignment.ShiftId,
                EnrollmentType = string.IsNullOrWhiteSpace(baseAssignment.EnrollmentType) ? "Regular" : baseAssignment.EnrollmentType,
                Status = "Active",
                IsActive = true,
                StartDate = DateTime.UtcNow
            };

            await AuditHelper.SetAuditFieldsForCreateAsync(enrollment, _currentUserService);
            await AuditHelper.SetSchoolIdAsync(enrollment, _currentUserService);
            _context.StudentSubjectAssignments.Add(enrollment);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Materia asignada correctamente." });
        }

        [HttpPost("/StudentAssignment/RemoveSubjectEnrollment")]
        public async Task<IActionResult> RemoveSubjectEnrollment(Guid enrollmentId)
        {
            if (enrollmentId == Guid.Empty)
                return Json(new { success = false, message = "Asignación inválida." });

            var enrollment = await _context.StudentSubjectAssignments.FirstOrDefaultAsync(ssa => ssa.Id == enrollmentId && ssa.IsActive);
            if (enrollment == null)
                return Json(new { success = false, message = "La asignación no existe o ya fue removida." });

            enrollment.IsActive = false;
            enrollment.Status = "Inactive";
            enrollment.EndDate = DateTime.UtcNow;
            await AuditHelper.SetAuditFieldsForUpdateAsync(enrollment, _currentUserService);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Materia removida correctamente." });
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

        [HttpGet("/StudentAssignment/UploadSubjectEnrollments")]
        public IActionResult UploadSubjectEnrollments()
        {
            // Vista dedicada a cargar el subconjunto de materias por estudiante (eje flexible para nocturna).
            return View();
        }

        private static string NormalizeToken(string? input)
        {
            input ??= string.Empty;
            input = input.Trim();
            input = input.Normalize(System.Text.NormalizationForm.FormD);
            input = new string(input.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            return input;
        }

        [HttpPost("/StudentAssignment/BulkSaveSubjectEnrollments")]
        public async Task<IActionResult> BulkSaveSubjectEnrollments([FromBody] List<StudentSubjectEnrollmentInputModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron filas." });

            var currentSchoolId = await GetCurrentUserSchoolId();
            if (currentSchoolId == null)
                return BadRequest(new { success = false, message = "No se pudo determinar la escuela actual." });

            var now = DateTime.UtcNow;
            var errors = new List<string>();

            // Resolución previa: convertimos cada fila a ids reales.
            // Además, construimos el “keepSet” (materias a mantener activas) por estudiante + (nivel, grupo, jornada/shift).
            var resolvedRows = new List<(Guid StudentId, Guid GradeId, Guid GroupId, Guid? ShiftId, Guid SubjectAssignmentId, bool Inscrito)>();
            var keepSetByKey = new Dictionary<string, HashSet<Guid>>();
            var enrollmentTypeByKey = new Dictionary<string, string>();

            foreach (var row in rows)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(row.EstudianteEmail))
                    {
                        errors.Add("EstudianteEmail es requerido.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(row.Asignatura) || string.IsNullOrWhiteSpace(row.Nivel) || string.IsNullOrWhiteSpace(row.GrupoAcademico))
                    {
                        errors.Add($"Fila inválida para {row.EstudianteEmail}: Asignatura/Nivel/GrupoAcadémico requeridos.");
                        continue;
                    }

                    // Crear/obtener usuario.
                    var student = await _userService.GetByEmailAsync(row.EstudianteEmail.Trim());
                    if (student == null)
                    {
                        student = new User
                        {
                            Id = Guid.NewGuid(),
                            Email = row.EstudianteEmail.Trim(),
                            Name = !string.IsNullOrWhiteSpace(row.Nombre) ? row.Nombre.Trim() : row.EstudianteEmail.Split('@')[0],
                            LastName = !string.IsNullOrWhiteSpace(row.Apellido) ? row.Apellido.Trim() : "Estudiante",
                            DocumentId = !string.IsNullOrWhiteSpace(row.DocumentoId)
                                ? row.DocumentoId.Trim()
                                : $"EST-{Guid.NewGuid().ToString("N")[..8]}",
                            DateOfBirth = null,
                            Role = "estudiante",
                            Status = "active",
                            CreatedAt = now,
                            UpdatedAt = now,
                            SchoolId = currentSchoolId,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                            TwoFactorEnabled = false,
                            LastLogin = null,
                            Inclusivo = null
                        };

                        await _userService.CreateAsync(student, new List<Guid>(), new List<Guid>());
                    }

                    var grade = await _gradeLevelService.GetByNameAsync(row.Nivel.Trim());
                    if (grade == null)
                    {
                        errors.Add($"Grado no encontrado: {row.Nivel} (estudiante {row.EstudianteEmail})");
                        continue;
                    }

                    Shift? shift = null;
                    if (!string.IsNullOrWhiteSpace(row.Jornada))
                    {
                        shift = await _shiftService.GetOrCreateAsync(row.Jornada.Trim());
                    }

                    var group = await _groupService.GetByNameAndGradeAsync(row.GrupoAcademico.Trim(), currentSchoolId, shift?.Id);
                    if (group == null)
                    {
                        errors.Add($"Grupo no encontrado: {row.GrupoAcademico} (estudiante {row.EstudianteEmail})");
                        continue;
                    }

                    // Alinear jornada al grupo si aplica (evita que AddEnrollmentAsync cree con shift distinto).
                    if (shift != null && (group.ShiftId == null || group.ShiftId != shift.Id))
                    {
                        group.ShiftId = shift.Id;
                        group.Shift = shift.Name;
                        group.UpdatedAt = now;
                        await _groupService.UpdateAsync(group);
                    }

                    // Materia (por Name o Code).
                    var normalizedSubject = NormalizeToken(row.Asignatura).ToUpperInvariant();
                    var subject = await _context.Subjects
                        .FirstOrDefaultAsync(s =>
                            (s.Name != null && NormalizeToken(s.Name).ToUpperInvariant() == normalizedSubject) ||
                            (s.Code != null && NormalizeToken(s.Code).ToUpperInvariant() == normalizedSubject));

                    if (subject == null)
                    {
                        errors.Add($"Materia no encontrada: {row.Asignatura} (estudiante {row.EstudianteEmail})");
                        continue;
                    }

                    // SubjectAssignment = (Subject + GradeLevel + Group)
                    var subjectAssignment = await _context.SubjectAssignments
                        .FirstOrDefaultAsync(sa =>
                            sa.SubjectId == subject.Id &&
                            sa.GradeLevelId == grade.Id &&
                            sa.GroupId == group.Id);

                    if (subjectAssignment == null)
                    {
                        errors.Add($"No existe SubjectAssignment para {row.Asignatura} | {row.Nivel} | {row.GrupoAcademico} (estudiante {row.EstudianteEmail}).");
                        continue;
                    }

                    var shiftId = group.ShiftId ?? shift?.Id;
                    var key = $"{student.Id}::{grade.Id}::{group.Id}::{shiftId?.ToString() ?? "null"}";
                    if (!keepSetByKey.TryGetValue(key, out var keepSet))
                    {
                        keepSet = new HashSet<Guid>();
                        keepSetByKey[key] = keepSet;
                    }

                    if (!enrollmentTypeByKey.ContainsKey(key))
                    {
                        var tipoMatricula = "Regular";
                        var jornadaNormalized = row.Jornada?.Trim().ToUpperInvariant() ?? string.Empty;
                        if (!string.IsNullOrEmpty(jornadaNormalized) && (jornadaNormalized.Contains("NOCHE") || jornadaNormalized.Contains("NOCTURNO")))
                            tipoMatricula = "Nocturno";
                        enrollmentTypeByKey[key] = tipoMatricula;
                    }

                    if (row.Inscrito)
                        keepSet.Add(subjectAssignment.Id);

                    resolvedRows.Add((student.Id, grade.Id, group.Id, shiftId, subjectAssignment.Id, row.Inscrito));
                }
                catch (Exception ex)
                {
                    errors.Add($"Error procesando fila ({row.EstudianteEmail}): {ex.Message}");
                }
            }

            if (resolvedRows.Count == 0)
            {
                return Ok(new
                {
                    success = true,
                    insertadas = 0,
                    desactivadas = 0,
                    errors,
                    message = "No se procesaron filas válidas."
                });
            }

            int activadas = 0;
            int desactivadas = 0;

            // Para cada “matrícula base” (estudiante + grado + grupo + shift), garantizamos:
            // 1) existe StudentAssignment
            // 2) StudentSubjectAssignments coincide con el keepSet (activar lo indicado, desactivar el resto)
            foreach (var kvp in keepSetByKey)
            {
                var key = kvp.Key;
                var keepSet = kvp.Value;

                // Parse key
                var parts = key.Split("::");
                var studentId = Guid.Parse(parts[0]);
                var gradeId = Guid.Parse(parts[1]);
                var groupId = Guid.Parse(parts[2]);
                Guid? shiftId = parts[3] == "null" ? null : Guid.Parse(parts[3]);

                var enrollmentType = enrollmentTypeByKey.TryGetValue(key, out var t) ? t : "Regular";

                // Asegurar StudentAssignment existente
                var shiftFilter = shiftId;
                var existsAssignment = await _studentAssignmentService.ExistsWithShiftAsync(studentId, gradeId, groupId, shiftFilter);
                if (!existsAssignment)
                {
                    // AddEnrollmentAsync disparará SyncStudentSubjectAssignmentsAsync (crea todas y luego afinamos con keepSet).
                    await _studentAssignmentService.AddEnrollmentAsync(studentId, gradeId, groupId, enrollmentType);
                }

                var baseAssignment = await _context.StudentAssignments
                    .Where(sa =>
                        sa.StudentId == studentId &&
                        sa.GradeId == gradeId &&
                        sa.GroupId == groupId &&
                        sa.IsActive)
                    .Where(sa => shiftId.HasValue ? sa.ShiftId == shiftId.Value : true)
                    .OrderByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
                    .FirstOrDefaultAsync();

                if (baseAssignment == null)
                {
                    errors.Add($"No se pudo determinar StudentAssignment base para el estudiante {studentId} | grado {gradeId} | grupo {groupId}.");
                    continue;
                }

                var academicYearId = baseAssignment.AcademicYearId;
                if (!academicYearId.HasValue)
                {
                    errors.Add($"AcademicYearId nulo para StudentAssignment base (student {studentId}, grade {gradeId}, group {groupId}).");
                    continue;
                }

                // Desactivar todo lo que no esté en keepSet (aunque falte StudentAssignmentId por datos históricos).
                var currentEnrollments = await _context.StudentSubjectAssignments
                    .Where(ssa => ssa.StudentId == studentId && ssa.IsActive && ssa.AcademicYearId == academicYearId.Value)
                    .Join(_context.SubjectAssignments,
                        ssa => ssa.SubjectAssignmentId,
                        sa => sa.Id,
                        (ssa, sa) => new { ssa, sa })
                    .Where(x => x.sa.GradeLevelId == gradeId && x.sa.GroupId == groupId)
                    .Select(x => x.ssa)
                    .ToListAsync();

                foreach (var enrollment in currentEnrollments)
                {
                    if (!keepSet.Contains(enrollment.SubjectAssignmentId))
                    {
                        enrollment.IsActive = false;
                        enrollment.Status = "Inactive";
                        enrollment.EndDate = now;
                        await AuditHelper.SetAuditFieldsForUpdateAsync(enrollment, _currentUserService);
                        desactivadas++;
                    }
                }

                // Activar (o asegurar) las materias del keepSet.
                foreach (var subjectAssignmentId in keepSet)
                {
                    var exists = await _context.StudentSubjectAssignments.AnyAsync(ssa =>
                        ssa.StudentId == studentId &&
                        ssa.SubjectAssignmentId == subjectAssignmentId &&
                        ssa.AcademicYearId == academicYearId.Value &&
                        ssa.IsActive);

                    if (exists)
                        continue;

                    var enrollment = new StudentSubjectAssignment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        SubjectAssignmentId = subjectAssignmentId,
                        StudentAssignmentId = baseAssignment.Id,
                        AcademicYearId = academicYearId.Value,
                        ShiftId = baseAssignment.ShiftId,
                        EnrollmentType = string.IsNullOrWhiteSpace(baseAssignment.EnrollmentType) ? "Regular" : baseAssignment.EnrollmentType,
                        Status = "Active",
                        IsActive = true,
                        StartDate = now,
                        CreatedAt = now
                    };

                    await AuditHelper.SetAuditFieldsForCreateAsync(enrollment, _currentUserService);
                    await AuditHelper.SetSchoolIdAsync(enrollment, _currentUserService);
                    _context.StudentSubjectAssignments.Add(enrollment);
                    activadas++;
                }

                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                success = true,
                insertadas = activadas,
                desactivadas,
                errors,
                message = "Carga de asignaturas individuales completada."
            });
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

            var currentSchoolId = await GetCurrentUserSchoolId();
            int insertadas = 0;
            int duplicadas = 0;
            int estudiantesCreados = 0;
            var errores = new List<string>();

            foreach (var item in asignaciones)
            {
                try
                {
                    Console.WriteLine($"[SaveAssignments] Procesando: {item.Estudiante} - {item.Grado} - {item.Grupo}");

                    // Solo nocturna: jornada vacía → "Noche" (resolución de grupo + tipo de matrícula coherente).
                    var jornadaParaShift = string.IsNullOrWhiteSpace(item.Jornada)
                        ? "Noche"
                        : item.Jornada.Trim();
                    var shift = await _shiftService.GetOrCreateAsync(jornadaParaShift);

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
                            SchoolId = currentSchoolId,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), // Contraseña temporal por defecto hasheada
                            TwoFactorEnabled = false,
                            LastLogin = null,
                            Inclusivo = item.Inclusivo,
                            Shift = jornadaParaShift // alineado con resolución de grupo (por defecto Noche)
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
                        student.Shift = jornadaParaShift;
                        student.UpdatedAt = DateTime.UtcNow;
                        
                        await _userService.UpdateAsync(student, new List<Guid>(), new List<Guid>());
                        
                        Console.WriteLine($"[SaveAssignments] Campos Inclusivo y Jornada actualizados para estudiante: {student.Id}, Jornada: {student.Shift}");
                    }

                    var grade = await _gradeLevelService.GetByNameAsync(item.Grado);

                    var group = await _groupService.GetByNameAndGradeAsync(item.Grupo, currentSchoolId, shift.Id);
                    // Si el grupo existe y no tiene jornada, asignarla al grupo
                    if (group != null && (group.ShiftId == null || group.ShiftId != shift.Id))
                    {
                        group.ShiftId = shift.Id;
                        group.Shift = shift.Name; // Mantener por compatibilidad
                        group.UpdatedAt = DateTime.UtcNow;
                        await _groupService.UpdateAsync(group);
                        Console.WriteLine($"[SaveAssignments] Jornada '{shift.Name}' (ID: {shift.Id}) asignada al grupo {group.Name}");
                    }

                    if (grade == null || group == null)
                    {
                        errores.Add($"Error de datos: {item.Estudiante} - {item.Grado} - {item.Grupo} (Grado o Grupo no encontrado)");
                        continue;
                    }

                    Console.WriteLine($"[SaveAssignments] Verificando si existe asignación: StudentId={student.Id}, GradeId={grade.Id}, GroupId={group.Id}, ShiftId={shift.Id}");
                    
                    bool exists = await _studentAssignmentService.ExistsWithShiftAsync(student.Id, grade.Id, group.Id, shift.Id);
                    if (exists)
                    {
                        Console.WriteLine($"[SaveAssignments] Asignación ya existe, saltando");
                        duplicadas++;
                        continue;
                    }

                    var tipoMatricula = string.IsNullOrWhiteSpace(item.TipoMatricula)
                        ? "Nocturno"
                        : item.TipoMatricula.Trim();
                    var assignment = new StudentAssignment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student.Id,
                        GradeId = grade.Id,
                        GroupId = group.Id,
                        ShiftId = shift.Id,
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
