using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Application.Interfaces;
using SchoolManager.Helpers;
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
        public async Task<IActionResult> UpdateGroupAndGrade(
            Guid studentId,
            Guid gradeId,
            Guid groupId,
            bool additive = false,
            bool forceReplaceAll = false)
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

            if (!forceReplaceAll)
                additive = true;

            var group = await _groupService.GetByIdAsync(groupId);
            if (group == null)
                return Json(new { success = false, message = "Grupo no válido." });

            var enrollmentType = EnrollmentTypeConstants.DefaultPrimary;

            if (!additive)
            {
                var activeCount = await _context.StudentAssignments
                    .CountAsync(sa => sa.StudentId == studentId && sa.IsActive);

                if (activeCount > 1 && !forceReplaceAll)
                {
                    return Json(new
                    {
                        success = false,
                        code = "MULTI_ENROLLMENT_CONFIRM",
                        activeCount,
                        message = "El estudiante tiene varias matrículas activas. Confirmar reemplazo total dejará una sola matrícula (las demás se inactivan)."
                    });
                }

                if (activeCount > 0)
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
                EnrollmentType = enrollmentType,
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
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            var catalog = await _studentAssignmentService.GetAvailableSubjectCatalogAsync(
                studentId, currentUser?.SchoolId, advancedMode: true);

            return Json(new
            {
                success = true,
                advancedMode = true,
                data = catalog.Select(c => new
                {
                    subjectAssignmentId = c.SubjectAssignmentId,
                    subjectName = c.SubjectName,
                    gradeName = c.GradeName,
                    groupName = c.GroupName,
                    display = c.Display,
                    isCarryOverGrade = c.IsCarryOverGrade,
                    requiresNewEnrollment = c.RequiresNewEnrollment
                })
            });
        }

        [HttpPost("/StudentAssignment/AddSubjectEnrollment")]
        public async Task<IActionResult> AddSubjectEnrollment(Guid studentId, Guid subjectAssignmentId, bool asCarryOver = false)
        {
            if (studentId == Guid.Empty || subjectAssignmentId == Guid.Empty)
                return Json(new { success = false, message = "Datos inválidos." });

            var result = await _studentAssignmentService.AddSubjectEnrollmentAsync(studentId, subjectAssignmentId, asCarryOver);
            return Json(new { success = result.Success, message = result.Message, enrollmentId = result.StudentSubjectAssignmentId });
        }

        [HttpPost("/StudentAssignment/AddCarryOverSubjectEnrollment")]
        public async Task<IActionResult> AddCarryOverSubjectEnrollment(Guid studentId, Guid subjectAssignmentId)
        {
            return await AddSubjectEnrollment(studentId, subjectAssignmentId, asCarryOver: true);
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
            var allShifts = await _shiftService.GetAllAsync();

            var assignmentsByStudent =
                await _studentAssignmentService.GetActiveAssignmentsForCurrentSchoolAsync();

            var gradeById = allGrades.ToDictionary(g => g.Id);
            var groupById = allGroups.ToDictionary(g => g.Id);
            var shiftById = allShifts.ToDictionary(s => s.Id);

            var viewModelList = new List<StudentAssignmentOverviewViewModel>();
            var gradesInUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var groupsInUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shiftsInUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var student in students)
            {
                assignmentsByStudent.TryGetValue(student.Id, out var assignments);
                assignments ??= new List<StudentAssignment>();

                var enrollmentItems = new List<StudentEnrollmentFilterItem>();
                var gradeGroupPairs = new List<string>();

                foreach (var a in assignments)
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

                    enrollmentItems.Add(new StudentEnrollmentFilterItem
                    {
                        Grade = gradeName,
                        Group = groupName,
                        Shift = shiftName
                    });

                    var tipo = EnrollmentTypeConstants.NormalizePrimary(a.EnrollmentType);
                    gradeGroupPairs.Add($"{gradeName} - {groupName} | Jornada: {shiftName} | Matrícula: {tipo}");
                }

                gradeGroupPairs = gradeGroupPairs.Distinct().ToList();
                enrollmentItems = enrollmentItems
                    .GroupBy(e => $"{e.Grade}|{e.Group}|{e.Shift}")
                    .Select(g => g.First())
                    .ToList();

                foreach (var e in enrollmentItems)
                {
                    if (!string.IsNullOrWhiteSpace(e.Grade) && e.Grade != "?")
                        gradesInUse.Add(e.Grade);
                    if (!string.IsNullOrWhiteSpace(e.Group) && e.Group != "?")
                        groupsInUse.Add(e.Group);
                    if (!string.IsNullOrWhiteSpace(e.Shift) && e.Shift != "Sin jornada")
                        shiftsInUse.Add(e.Shift);
                }

                viewModelList.Add(new StudentAssignmentOverviewViewModel
                {
                    StudentId = student.Id,
                    FullName = student.Name,
                    FirstName = student.Name,
                    LastName = student.LastName ?? "",
                    DocumentId = student.DocumentId ?? "",
                    Email = student.Email,
                    IsActive = string.Equals(student.Status, "active", StringComparison.OrdinalIgnoreCase),
                    GradeGroupPairs = gradeGroupPairs,
                    Enrollments = enrollmentItems
                });
            }

            static List<string> MergeFilterOptions(IEnumerable<string> fromCatalog, HashSet<string> inUse)
            {
                return fromCatalog
                    .Concat(inUse)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var indexModel = new StudentAssignmentIndexViewModel
            {
                Students = viewModelList,
                GradeFilterOptions = MergeFilterOptions(allGrades.Select(g => g.Name), gradesInUse),
                GroupFilterOptions = MergeFilterOptions(allGroups.Select(g => g.Name), groupsInUse),
                ShiftFilterOptions = MergeFilterOptions(allShifts.Select(s => s.Name), shiftsInUse)
            };

            return View(indexModel);
        }

        public async Task<IActionResult> Upload()
        {
            var ctx = await SchoolTenantHelper.TryGetBulkUploadSchoolContextAsync(_context, _currentUserService);
            ViewBag.BulkUploadSchoolName = ctx?.SchoolName;
            ViewBag.BulkUploadSchoolId = ctx?.SchoolId;
            ViewBag.BulkUploadBlocked = ctx == null;
            return View();
        }

        private static string NormalizeToken(string? input) =>
            BulkNightEnrollmentHelper.NormalizeCatalogToken(input);

        private async Task<Subject?> ResolveSubjectForBulkAsync(
            Guid schoolId,
            string rawName,
            IReadOnlyList<Subject> schoolSubjects)
        {
            var lookupName = BulkNightEnrollmentHelper.ResolveSubjectLookupName(rawName);
            var normalized = BulkNightEnrollmentHelper.NormalizeSubjectKey(lookupName);

            var subject = schoolSubjects.FirstOrDefault(s =>
                BulkNightEnrollmentHelper.NormalizeSubjectKey(s.Name) == normalized ||
                (s.Code != null && BulkNightEnrollmentHelper.NormalizeSubjectKey(s.Code) == normalized));
            if (subject != null)
                return subject;

            subject = schoolSubjects.FirstOrDefault(s =>
            {
                var dbName = BulkNightEnrollmentHelper.NormalizeSubjectKey(s.Name);
                return dbName.StartsWith(normalized, StringComparison.Ordinal) ||
                       normalized.StartsWith(dbName, StringComparison.Ordinal);
            });
            if (subject != null)
                return subject;

            return await _subjectService.GetOrCreateAsync(lookupName);
        }

        private async Task<User?> ResolveStudentForBulkAsync(
            StudentSubjectEnrollmentInputModel row,
            Guid schoolId,
            DateTime now,
            Dictionary<string, User> studentsByEmail)
        {
            var emailKey = row.EstudianteEmail.Trim().ToLowerInvariant();
            if (studentsByEmail.TryGetValue(emailKey, out var cached))
                return cached;

            var student = await _userService.GetByEmailIgnoringTenantAsync(row.EstudianteEmail.Trim());
            if (student != null && !SchoolTenantHelper.UserBelongsToSchool(student, schoolId))
                return null;

            if (student == null && !string.IsNullOrWhiteSpace(row.DocumentoId))
            {
                var doc = row.DocumentoId.Trim();
                student = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u =>
                        u.DocumentId == doc &&
                        u.SchoolId == schoolId &&
                        (u.Role ?? string.Empty).ToLower() == "estudiante");
            }

            if (student == null)
            {
                var docId = !string.IsNullOrWhiteSpace(row.DocumentoId)
                    ? row.DocumentoId.Trim()
                    : $"EST-{Guid.NewGuid().ToString("N")[..8]}";

                if (!string.IsNullOrWhiteSpace(row.DocumentoId))
                {
                    var docTaken = await _context.Users
                        .IgnoreQueryFilters()
                        .AnyAsync(u => u.DocumentId == docId);
                    if (docTaken)
                        throw new InvalidOperationException(
                            $"Documento ID '{docId}' ya está registrado para otro usuario.");
                }

                student = new User
                {
                    Id = Guid.NewGuid(),
                    Email = row.EstudianteEmail.Trim(),
                    Name = !string.IsNullOrWhiteSpace(row.Nombre) ? row.Nombre.Trim() : row.EstudianteEmail.Split('@')[0],
                    LastName = !string.IsNullOrWhiteSpace(row.Apellido) ? row.Apellido.Trim() : "Estudiante",
                    DocumentId = docId,
                    DateOfBirth = null,
                    Role = "estudiante",
                    Status = "active",
                    CreatedAt = now,
                    UpdatedAt = now,
                    SchoolId = schoolId,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    TwoFactorEnabled = false,
                    LastLogin = null,
                    Inclusivo = null,
                    Shift = "Noche"
                };

                await _userService.CreateAsync(student, new List<Guid>(), new List<Guid>());
            }

            studentsByEmail[emailKey] = student;
            return student;
        }

        private static string FormatSaveError(Exception ex)
        {
            var current = ex;
            while (current.InnerException != null)
                current = current.InnerException;
            return current.Message;
        }

        private static void DetachPendingAddedEntities(SchoolDbContext context)
        {
            foreach (var entry in context.ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        /// <summary>
        /// Si no existe impartición materia+grado+grupo, la crea reutilizando área y especialidad de otra impartición de la escuela (o global).
        /// </summary>
        private async Task<(SubjectAssignment? Assignment, string? Error)> EnsureSubjectAssignmentForBulkAsync(
            Guid schoolId,
            Guid subjectId,
            Guid gradeLevelId,
            Guid groupId)
        {
            var existing = await _context.SubjectAssignments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(sa =>
                    sa.SubjectId == subjectId &&
                    sa.GradeLevelId == gradeLevelId &&
                    sa.GroupId == groupId &&
                    (sa.SchoolId == null || sa.SchoolId == schoolId));
            if (existing != null)
                return (existing, null);

            var template = await _context.SubjectAssignments.AsNoTracking()
                .Where(sa => sa.SchoolId == schoolId)
                .Select(sa => new { sa.AreaId, sa.SpecialtyId })
                .FirstOrDefaultAsync();

            if (template == null)
            {
                template = await _context.SubjectAssignments.AsNoTracking()
                    .Select(sa => new { sa.AreaId, sa.SpecialtyId })
                    .FirstOrDefaultAsync();
            }

            if (template == null)
                return (null, "No hay imparticiones (SubjectAssignment) para usar como plantilla de área/especialidad.");

            var created = new SubjectAssignment
            {
                Id = Guid.NewGuid(),
                SubjectId = subjectId,
                GradeLevelId = gradeLevelId,
                GroupId = groupId,
                AreaId = template.AreaId,
                SpecialtyId = template.SpecialtyId,
                SchoolId = schoolId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _context.SubjectAssignments.Add(created);
            await _context.SaveChangesAsync();
            return (created, null);
        }

        private async Task<IActionResult> ProcessBulkSubjectEnrollmentsAsync(List<StudentSubjectEnrollmentInputModel> rows)
        {
            if (rows == null || rows.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron filas." });

            rows = BulkNightEnrollmentHelper.DedupeRows(rows);

            var currentSchoolId = await GetCurrentUserSchoolId();
            if (currentSchoolId == null)
                return BadRequest(new { success = false, message = "No se pudo determinar la escuela actual." });

            var schoolId = currentSchoolId.Value;
            var now = DateTime.UtcNow;
            var errors = new List<string>();
            var studentsByEmail = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
            var schoolSubjects = await _context.Subjects
                .Where(s => s.SchoolId == schoolId)
                .ToListAsync();

            var pendingRows = new List<(
                Guid StudentId,
                Guid GradeId,
                string GradeName,
                Guid GroupId,
                Guid? ShiftId,
                Guid SubjectAssignmentId,
                bool Inscrito,
                string? TipoInscripcion)>();

            var pendingKeys = new HashSet<string>(StringComparer.Ordinal);

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

                    var student = await ResolveStudentForBulkAsync(row, schoolId, now, studentsByEmail);
                    if (student == null)
                    {
                        errors.Add($"El correo {row.EstudianteEmail} pertenece a otra institución.");
                        continue;
                    }

                    var nivelLabel = BulkNightEnrollmentHelper.NormalizeGradeLabel(row.Nivel);
                    var grade = await _gradeLevelService.ResolveByNameAsync(nivelLabel);
                    if (grade == null)
                        grade = await _gradeLevelService.GetOrCreateAsync(nivelLabel);

                    // Plataforma solo nocturna: la jornada del Excel no cambia el turno efectivo.
                    const string jornadaEfectiva = "Noche";
                    var shift = await _shiftService.GetOrCreateBySchoolAndNameAsync(schoolId, jornadaEfectiva);

                    var group = await _groupService.GetByNameAndGradeAsync(row.GrupoAcademico.Trim(), currentSchoolId, shift.Id);
                    if (group == null)
                        group = await _groupService.GetByNameAndGradeAsync(row.GrupoAcademico.Trim(), currentSchoolId);
                    if (group == null)
                    {
                        errors.Add($"Grupo no encontrado: {row.GrupoAcademico} (estudiante {row.EstudianteEmail}). Cree el grupo en su escuela o verifique el nombre.");
                        continue;
                    }

                    if (group.ShiftId == null || group.ShiftId != shift.Id)
                    {
                        group.ShiftId = shift.Id;
                        group.Shift = shift.Name;
                        group.UpdatedAt = now;
                        await _groupService.UpdateAsync(group);
                    }

                    var subject = await ResolveSubjectForBulkAsync(schoolId, row.Asignatura.Trim(), schoolSubjects);
                    if (subject == null)
                    {
                        errors.Add($"No se pudo resolver la asignatura '{row.Asignatura}' ({row.EstudianteEmail}).");
                        continue;
                    }

                    if (!schoolSubjects.Any(s => s.Id == subject.Id))
                        schoolSubjects = schoolSubjects.Append(subject).ToList();

                    var subjectAssignment = await _context.SubjectAssignments
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(sa =>
                            sa.SubjectId == subject.Id &&
                            sa.GradeLevelId == grade.Id &&
                            sa.GroupId == group.Id &&
                            (sa.SchoolId == null || sa.SchoolId == schoolId));

                    if (subjectAssignment == null)
                    {
                        var (createdSa, ensureErr) = await EnsureSubjectAssignmentForBulkAsync(schoolId, subject.Id, grade.Id, group.Id);
                        if (createdSa == null)
                        {
                            errors.Add($"{ensureErr} Fila: {row.Asignatura} | {row.Nivel} | {row.GrupoAcademico} ({row.EstudianteEmail}).");
                            continue;
                        }

                        subjectAssignment = createdSa;
                    }

                    var pendingKey = $"{student.Id}::{subjectAssignment.Id}::{grade.Id}::{group.Id}";
                    if (!pendingKeys.Add(pendingKey))
                        continue;

                    pendingRows.Add((
                        student.Id,
                        grade.Id,
                        grade.Name,
                        group.Id,
                        group.ShiftId ?? shift.Id,
                        subjectAssignment.Id,
                        row.Inscrito,
                        row.TipoInscripcion));
                }
                catch (Exception ex)
                {
                    DetachPendingAddedEntities(_context);
                    errors.Add($"Error procesando fila ({row.EstudianteEmail}): {FormatSaveError(ex)}");
                }
            }

            if (pendingRows.Count == 0)
            {
                return Ok(new
                {
                    success = true,
                    insertadas = 0,
                    desactivadas = 0,
                    errores = errors,
                    message = "No se procesaron filas válidas."
                });
            }

            var studentIds = pendingRows.Select(r => r.StudentId).Distinct().ToList();
            var primaryGradeIdsByStudent = new Dictionary<Guid, HashSet<Guid>>();

            var existingPrimaryGrades = await _context.StudentAssignments
                .Where(sa => studentIds.Contains(sa.StudentId) && sa.IsActive)
                .Select(sa => new { sa.StudentId, sa.GradeId, sa.EnrollmentType })
                .ToListAsync();

            foreach (var sa in existingPrimaryGrades.Where(x => EnrollmentTypeConstants.IsPrimaryLevel(x.EnrollmentType)))
            {
                if (!primaryGradeIdsByStudent.TryGetValue(sa.StudentId, out var set))
                {
                    set = new HashSet<Guid>();
                    primaryGradeIdsByStudent[sa.StudentId] = set;
                }
                set.Add(sa.GradeId);
            }

            foreach (var groupRows in pendingRows.Where(r => r.Inscrito).GroupBy(r => r.StudentId))
            {
                var studentId = groupRows.Key;
                if (!primaryGradeIdsByStudent.TryGetValue(studentId, out var primarySet))
                {
                    primarySet = new HashSet<Guid>();
                    primaryGradeIdsByStudent[studentId] = primarySet;
                }

                foreach (var row in groupRows.Where(r =>
                             !string.IsNullOrWhiteSpace(r.TipoInscripcion) &&
                             r.TipoInscripcion.Trim().Equals(EnrollmentTypeConstants.Nocturno, StringComparison.OrdinalIgnoreCase)))
                {
                    primarySet.Add(row.GradeId);
                }

                if (primarySet.Count == 0)
                {
                    var candidate = groupRows
                        .Where(r => string.IsNullOrWhiteSpace(r.TipoInscripcion) ||
                                    !EnrollmentTypeConstants.IsCarryOver(r.TipoInscripcion))
                        .Select(r => new { r.GradeId, Level = EnrollmentTypeConstants.ParseGradeNumber(r.GradeName) ?? -1 })
                        .OrderByDescending(x => x.Level)
                        .FirstOrDefault();

                    if (candidate != null)
                        primarySet.Add(candidate.GradeId);
                }
            }

            var keepSetByKey = new Dictionary<string, HashSet<Guid>>();
            var enrollmentTypeByKey = new Dictionary<string, string>();
            var ssaEnrollmentTypeBySubject = new Dictionary<string, string>();

            foreach (var row in pendingRows)
            {
                var key = $"{row.StudentId}::{row.GradeId}::{row.GroupId}::{row.ShiftId}";

                primaryGradeIdsByStudent.TryGetValue(row.StudentId, out var primaryGrades);
                primaryGrades ??= new HashSet<Guid>();

                var isCarryOverGrade = EnrollmentTypeConstants.IsCarryOver(row.TipoInscripcion) ||
                                       (primaryGrades.Count > 0 && !primaryGrades.Contains(row.GradeId));

                var ssaType = EnrollmentTypeConstants.ResolveSubjectEnrollmentType(row.TipoInscripcion, isCarryOverGrade);
                var baseType = isCarryOverGrade
                    ? EnrollmentTypeConstants.Refuerzo
                    : EnrollmentTypeConstants.DefaultPrimary;

                if (!keepSetByKey.TryGetValue(key, out var keepSet))
                {
                    keepSet = new HashSet<Guid>();
                    keepSetByKey[key] = keepSet;
                }

                enrollmentTypeByKey[key] = baseType;
                ssaEnrollmentTypeBySubject[$"{row.StudentId}::{row.SubjectAssignmentId}"] = ssaType;

                if (row.Inscrito)
                    keepSet.Add(row.SubjectAssignmentId);
            }

            int activadas = 0;
            int desactivadas = 0;

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

                var enrollmentType = enrollmentTypeByKey.TryGetValue(key, out var t)
                    ? t
                    : EnrollmentTypeConstants.DefaultPrimary;

                var shiftFilter = shiftId;
                var existsAssignment = await _studentAssignmentService.ExistsWithShiftAsync(studentId, gradeId, groupId, shiftFilter);
                if (!existsAssignment)
                    await _studentAssignmentService.AddEnrollmentAsync(studentId, gradeId, groupId, enrollmentType);

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

                if (!string.Equals(baseAssignment.EnrollmentType, enrollmentType, StringComparison.OrdinalIgnoreCase))
                {
                    baseAssignment.EnrollmentType = enrollmentType;
                    await AuditHelper.SetAuditFieldsForUpdateAsync(baseAssignment, _currentUserService);
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
                    else if (ssaEnrollmentTypeBySubject.TryGetValue($"{studentId}::{enrollment.SubjectAssignmentId}", out var existingType) &&
                             !string.Equals(enrollment.EnrollmentType, existingType, StringComparison.OrdinalIgnoreCase))
                    {
                        enrollment.EnrollmentType = existingType;
                        await AuditHelper.SetAuditFieldsForUpdateAsync(enrollment, _currentUserService);
                    }
                }

                foreach (var subjectAssignmentId in keepSet)
                {
                    var ssaType = ssaEnrollmentTypeBySubject.TryGetValue($"{studentId}::{subjectAssignmentId}", out var resolvedType)
                        ? resolvedType
                        : EnrollmentTypeConstants.NormalizePrimary(baseAssignment.EnrollmentType);

                    var existing = await _context.StudentSubjectAssignments
                        .Where(ssa =>
                            ssa.StudentId == studentId &&
                            ssa.SubjectAssignmentId == subjectAssignmentId &&
                            ssa.AcademicYearId == academicYearId.Value)
                        .OrderByDescending(ssa => ssa.IsActive)
                        .ThenByDescending(ssa => ssa.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        if (!existing.IsActive)
                        {
                            existing.IsActive = true;
                            existing.Status = "Active";
                            existing.EndDate = null;
                            existing.StudentAssignmentId = baseAssignment.Id;
                            existing.ShiftId = baseAssignment.ShiftId;
                            existing.StartDate = now;
                            await AuditHelper.SetAuditFieldsForUpdateAsync(existing, _currentUserService);
                            activadas++;
                        }

                        if (!string.Equals(existing.EnrollmentType, ssaType, StringComparison.OrdinalIgnoreCase))
                        {
                            existing.EnrollmentType = ssaType;
                            await AuditHelper.SetAuditFieldsForUpdateAsync(existing, _currentUserService);
                        }
                        continue;
                    }

                    var enrollment = new StudentSubjectAssignment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        SubjectAssignmentId = subjectAssignmentId,
                        StudentAssignmentId = baseAssignment.Id,
                        AcademicYearId = academicYearId.Value,
                        ShiftId = baseAssignment.ShiftId,
                        EnrollmentType = ssaType,
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
                errores = errors,
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
        private async Task<Guid?> GetCurrentUserSchoolId()
        {
            try
            {
                // No usar User.Identity.Name: en cookie auth suele ser el nombre (ClaimTypes.Name), no el email.
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                return currentUser?.SchoolId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCurrentUserSchoolId] Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>Carga masiva: la UI usa <c>mode=subjects</c>; <c>mode=gradeGroup</c> queda por compatibilidad con clientes antiguos.</summary>
        [HttpPost("/StudentAssignment/SaveAssignments")]
        public async Task<IActionResult> SaveAssignments([FromBody] BulkStudentUploadRequest? request)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "Solicitud inválida." });

            var mode = (request.Mode ?? "gradeGroup").Trim();
            if (string.Equals(mode, "subjects", StringComparison.OrdinalIgnoreCase))
            {
                if (request.SubjectRows == null || request.SubjectRows.Count == 0)
                    return BadRequest(new { success = false, message = "No se recibieron filas de materias." });
                try
                {
                    return await ProcessBulkSubjectEnrollmentsAsync(request.SubjectRows);
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"[SaveAssignments] subjects DbUpdateException: {dbEx}");
                    var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                    return StatusCode(500, new { success = false, message = "Error al guardar en la base de datos.", detail = inner });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SaveAssignments] subjects Exception: {ex}");
                    return StatusCode(500, new { success = false, message = "Error al procesar la carga masiva.", detail = ex.Message });
                }
            }

            if (request.GradeGroupRows == null || request.GradeGroupRows.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron asignaciones." });

            return await ProcessBulkGradeGroupMatriculasAsync(request.GradeGroupRows);
        }

        private async Task<IActionResult> ProcessBulkGradeGroupMatriculasAsync(List<StudentAssignmentInputModel> asignaciones)
        {
            var currentSchoolId = await GetCurrentUserSchoolId();
            if (currentSchoolId == null)
                return BadRequest(new { success = false, message = "No se pudo determinar la escuela actual." });

            var schoolId = currentSchoolId.Value;
            int insertadas = 0;
            int duplicadas = 0;
            int estudiantesCreados = 0;
            var errores = new List<string>();

            foreach (var item in asignaciones)
            {
                try
                {
                    Console.WriteLine($"[SaveAssignments] Procesando: {item.Estudiante} - {item.Grado} - {item.Grupo}");

                    // Plataforma solo nocturna: siempre turno Noche para matrícula y grupo.
                    const string jornadaParaShift = "Noche";
                    var shift = await _shiftService.GetOrCreateBySchoolAndNameAsync(schoolId, jornadaParaShift);

                    // Buscar o crear el estudiante
                    var student = await _userService.GetByEmailIgnoringTenantAsync(item.Estudiante);
                    if (student != null && !SchoolTenantHelper.UserBelongsToSchool(student, schoolId))
                    {
                        errores.Add($"El correo {item.Estudiante} pertenece a otra institución.");
                        continue;
                    }

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
                            SchoolId = schoolId,
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

                    var grade = await _gradeLevelService.ResolveByNameAsync(item.Grado);
                    if (grade == null)
                        grade = await _gradeLevelService.GetByNameAsync(item.Grado);

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
                        ? EnrollmentTypeConstants.DefaultPrimary
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
