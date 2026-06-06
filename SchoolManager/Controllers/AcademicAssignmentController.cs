using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Application.Interfaces;
using SchoolManager.Helpers;
using SchoolManager.Infrastructure.Services;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;

[Authorize(Roles = "admin,secretaria")]
public class AcademicAssignmentController : Controller
{
    private readonly ITeacherAssignmentService _teacherAssignmentService;
    private readonly IUserService _userService;
    private readonly ISubjectService _subjectService;
    private readonly IGroupService _groupService;
    private readonly IGradeLevelService _gradeLevelService;
    private readonly IAcademicAssignmentService _academicAssignmentService;
    private readonly IAreaService _areaService;
    private readonly ISpecialtyService _specialtyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeHomologationService _dateTimeHomologationService;
    private readonly SchoolDbContext _context;

    public AcademicAssignmentController(
        ITeacherAssignmentService teacherAssignmentService,
        IUserService userService,
        ISubjectService subjectService,
        IGroupService groupService,
        IGradeLevelService gradeLevelService,
        IAcademicAssignmentService academicAssignmentService,
        IAreaService areaService,
        ISpecialtyService specialtyService,
        ICurrentUserService currentUserService,
        IDateTimeHomologationService dateTimeHomologationService,
        SchoolDbContext context)
    {
        _teacherAssignmentService = teacherAssignmentService;
        _userService = userService;
        _subjectService = subjectService;
        _groupService = groupService;
        _gradeLevelService = gradeLevelService;
        _academicAssignmentService = academicAssignmentService;
        _areaService = areaService;
        _specialtyService = specialtyService;
        _currentUserService = currentUserService;
        _dateTimeHomologationService = dateTimeHomologationService;
        _context = context;
    }


    public async Task<IActionResult> Upload()
    {
        var ctx = await SchoolTenantHelper.TryGetBulkUploadSchoolContextAsync(_context, _currentUserService);
        ViewBag.BulkUploadSchoolName = ctx?.SchoolName;
        ViewBag.BulkUploadSchoolId = ctx?.SchoolId;
        ViewBag.BulkUploadBlocked = ctx == null;
        return View();
    }

    // Carga masiva desde archivo Excel
    [HttpPost]
    public async Task<IActionResult> SaveAssignmentsFromExcel([FromBody] List<AssignmentInputModel> asignaciones)
    {
        if (asignaciones == null || !asignaciones.Any())
            return BadRequest(new { message = "No se recibió información válida." });

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var schoolId = currentUser?.SchoolId;
        if (schoolId == null || schoolId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "No se pudo determinar la escuela del usuario. Use una cuenta de administrador del colegio."
            });
        }

        var asignacionesInsertadas = 0;
        var duplicadasEnArchivo = 0;
        var duplicadasEnBd = 0;
        var errores = new List<string>();
        var combinacionesEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var profesoresCreados = new List<string>();

        for (var index = 0; index < asignaciones.Count; index++)
        {
            var asignacion = asignaciones[index];
            var fila = index + 1;

            // Normalizar entradas
            var especialidad = string.IsNullOrWhiteSpace(asignacion.Especialidad) ? "N/A" : asignacion.Especialidad.Trim().ToUpper();
            var area = string.IsNullOrWhiteSpace(asignacion.Area) ? "N/A" : asignacion.Area.Trim().ToUpper();
            var materia = string.IsNullOrWhiteSpace(asignacion.Materia) ? "N/A" : asignacion.Materia.Trim().ToUpper();
            var grado = string.IsNullOrWhiteSpace(asignacion.Grado) ? "N/A" : asignacion.Grado.Trim().ToUpper();
            var grupo = string.IsNullOrWhiteSpace(asignacion.Grupo) ? "N/A" : asignacion.Grupo.Trim().ToUpper();
            var combinacionKey = $"{especialidad}|{area}|{materia}|{grado}|{grupo}";

            if (!combinacionesEnArchivo.Add(combinacionKey))
            {
                duplicadasEnArchivo++;
                errores.Add($"Fila {fila}: Combinación duplicada en el archivo ({especialidad} / {area} / {materia} / {grado} / {grupo}).");
                continue;
            }
            
            // Usar EmailDocente si está disponible, sino usar Docente (compatibilidad)
            var correoDocente = !string.IsNullOrWhiteSpace(asignacion.EmailDocente) 
                ? asignacion.EmailDocente.Trim().ToLower() 
                : asignacion.Docente?.Trim().ToLower();

            var specialty = await _specialtyService.GetOrCreateAsync(especialidad);
            var areaEntity = await _areaService.GetOrCreateAsync(area);
            var subject = await _subjectService.GetOrCreateAsync(materia);
            var grade = await _gradeLevelService.GetOrCreateAsync(grado);
            var groupEntity = await _groupService.GetOrCreateAsync(grupo);

            var yaExiste = await _academicAssignmentService.ExisteAsignacionAsync(
                specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id, schoolId
            );

            if (yaExiste)
            {
                duplicadasEnBd++;
                errores.Add($"Fila {fila}: La combinación ya existe en el sistema ({especialidad} / {area} / {materia} / {grado} / {grupo}).");
            }
            else
            {
                await _academicAssignmentService.CreateAsignacionAsync(
                    specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id, schoolId
                );
                asignacionesInsertadas++;
            }

            // Obtener el subject_assignment_id
            var subjectAssignmentId = await _academicAssignmentService.GetSubjectAssignmentIdAsync(
                specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id, schoolId
            );

            if (subjectAssignmentId != null && !string.IsNullOrEmpty(correoDocente))
            {
                // Buscar docente (global: validar escuela)
                var docente = await _userService.GetByEmailIgnoringTenantAsync(correoDocente);
                if (docente != null)
                {
                    if (!SchoolTenantHelper.UserBelongsToSchool(docente, schoolId.Value))
                    {
                        errores.Add($"Fila {fila}: El docente {correoDocente} pertenece a otra institución.");
                        continue;
                    }

                    await _academicAssignmentService.AssignTeacherAsync(docente.Id, subjectAssignmentId.Value);
                }
                else
                {
                    // Verificar nuevamente que no existe antes de crear (doble validación)
                    var docenteVerificado = await _userService.GetByEmailIgnoringTenantAsync(correoDocente);
                    if (docenteVerificado == null)
                    {
                        // Usar datos del Excel o valores por defecto
                        var nombreProfesor = !string.IsNullOrWhiteSpace(asignacion.Nombre) 
                            ? asignacion.Nombre.Trim() 
                            : "Profesor";
                        var apellidoProfesor = !string.IsNullOrWhiteSpace(asignacion.Apellido) 
                            ? asignacion.Apellido.Trim() 
                            : "Nuevo";
                        var documentoProfesor = !string.IsNullOrWhiteSpace(asignacion.DocumentoId) 
                            ? asignacion.DocumentoId.Trim() 
                            : null;
                        
                        // Homologar fecha de nacimiento usando el servicio especializado
                        DateTime fechaNacimiento = _dateTimeHomologationService.HomologateDateOfBirth(
                            asignacion.FechaNacimiento, 
                            "AcademicAssignment"
                        );

                        // Crear nuevo profesor automáticamente
                        var nuevoProfesor = new User
                        {
                            Id = Guid.NewGuid(),
                            Email = correoDocente,
                            Name = nombreProfesor,
                            LastName = apellidoProfesor,
                            DocumentId = documentoProfesor,
                            DateOfBirth = fechaNacimiento,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                            Role = "teacher", // Usar minúscula para coincidir con la restricción CHECK
                            SchoolId = schoolId,
                            Status = "active",
                            CreatedAt = DateTime.UtcNow,
                            TwoFactorEnabled = false
                        };

                        await _userService.CreateAsync(nuevoProfesor, new List<Guid>(), new List<Guid>());
                        profesoresCreados.Add(correoDocente);
                        
                        // Asignar el profesor recién creado
                        await _academicAssignmentService.AssignTeacherAsync(nuevoProfesor.Id, subjectAssignmentId.Value);
                    }
                    else
                    {
                        if (!SchoolTenantHelper.UserBelongsToSchool(docenteVerificado, schoolId.Value))
                        {
                            errores.Add($"Fila {fila}: El docente {correoDocente} pertenece a otra institución.");
                            continue;
                        }

                        await _academicAssignmentService.AssignTeacherAsync(docenteVerificado.Id, subjectAssignmentId.Value);
                    }
                }
            }
        }

        return Ok(new
        {
            message = asignacionesInsertadas > 0
                ? $"Se insertaron {asignacionesInsertadas} nuevas asignaciones."
                : "No se insertaron nuevas asignaciones.",
            asignacionesInsertadas,
            duplicadasEnArchivo,
            duplicadasEnBd,
            errores,
            profesoresCreados = profesoresCreados,
            profesoresCreadosCount = profesoresCreados.Count,
            success = true
        });
    }

    //public async Task<IActionResult> SaveAssignmentsFromExcel([FromBody] List<AssignmentInputModel> asignaciones)
    //{
    //    if (asignaciones == null || !asignaciones.Any())
    //        return BadRequest(new { message = "No se recibió información válida." });

    //    var asignacionesInsertadas = 0;

    //    foreach (var asignacion in asignaciones)
    //    {
    //        // Normalización básica (espacios y mayúsculas)
    //        // Normalización con valor por defecto si está vacío o nulo
    //        var especialidad = string.IsNullOrWhiteSpace(asignacion.Especialidad) ? "N/A" : asignacion.Especialidad.Trim().ToUpper();
    //        var area = string.IsNullOrWhiteSpace(asignacion.Area) ? "N/A" : asignacion.Area.Trim().ToUpper();
    //        var materia = string.IsNullOrWhiteSpace(asignacion.Materia) ? "N/A" : asignacion.Materia.Trim().ToUpper();
    //        var grado = string.IsNullOrWhiteSpace(asignacion.Grado) ? "N/A" : asignacion.Grado.Trim().ToUpper();
    //        var grupo = string.IsNullOrWhiteSpace(asignacion.Grupo) ? "N/A" : asignacion.Grupo.Trim().ToUpper();

    //        var specialty = await _specialtyService.GetOrCreateAsync(especialidad);
    //        var areaEntity = await _areaService.GetOrCreateAsync(area);
    //        var subject = await _subjectService.GetOrCreateAsync(materia);
    //        var grade = await _gradeLevelService.GetOrCreateAsync(grado);
    //        var groupEntity = await _groupService.GetOrCreateAsync(grupo);

    //        bool yaExiste = await _academicAssignmentService.ExisteAsignacionAsync(
    //            specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id
    //        );

    //        if (!yaExiste)
    //        {
    //            await _academicAssignmentService.CreateAsignacionAsync(
    //                specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id
    //            );
    //            asignacionesInsertadas++;
    //        }
    //    }

    //    return Ok(new
    //    {
    //        message = asignacionesInsertadas > 0
    //            ? $"Se insertaron {asignacionesInsertadas} nuevas asignaciones."
    //            : "No se insertaron nuevas asignaciones. Todas ya existían.",
    //        success = true
    //    });
    //}

    //public async Task<IActionResult> SaveAssignmentsFromExcel([FromBody] List<AssignmentInputModel> asignaciones)
    //{
    //    if (asignaciones == null || !asignaciones.Any())
    //        return BadRequest(new { message = "No se recibió información válida." });

    //    var asignacionesInsertadas = 0;


    //    foreach (var asignacion in asignaciones)
    //    {


    //        var specialty = await _specialtyService.GetOrCreateAsync(asignacion.Especialidad);
    //        var area = await _areaService.GetOrCreateAsync(asignacion.Area);
    //        var subject = await _subjectService.GetOrCreateAsync(asignacion.Materia);
    //        var grade = await _gradeLevelService.GetOrCreateAsync(asignacion.Grado);
    //        var group = await _groupService.GetOrCreateAsync(asignacion.Grupo);

    //        // Validar si ya existe la asignación completa
    //        bool yaExiste = await _academicAssignmentService.ExisteAsignacionAsync(
    //            specialty.Id, area.Id, subject.Id, grade.Id, group.Id
    //        );

    //        if (!yaExiste)
    //        {
    //            await _academicAssignmentService.CreateAsignacionAsync(
    //                specialty.Id, area.Id, subject.Id, grade.Id, group.Id
    //            );
    //            asignacionesInsertadas++;
    //        }
    //    }

    //    return Ok(new
    //    {
    //        message = asignacionesInsertadas > 0
    //            ? $"Se insertaron {asignacionesInsertadas} nuevas asignaciones."
    //            : "No se insertaron nuevas asignaciones. Todas ya existían.",
    //        success = true
    //    });
    //}
    //// Mostrar listado de profesores y formulario de asignación
    public async Task<IActionResult> Index()
    {
        var user = await _userService.GetAllTeachersAsync();
        ViewBag.Subjects = await _subjectService.GetAllAsync();
        ViewBag.Groups = await _groupService.GetAllAsync();
        ViewBag.Grades = await _gradeLevelService.GetAllAsync();

        return View(user);
    }

    [HttpGet("Assign")]
    public async Task<IActionResult> Assign(Guid id)
    {
        var teacher = await _userService.GetByIdAsync(id);
        var subjects = (await _subjectService.GetAllAsync()).ToList();
        var grades = (await _gradeLevelService.GetAllAsync()).ToList();
        var groups = (await _groupService.GetAllAsync()).ToList();

        var viewModel = new AssignViewModel
        {
            Teacher = teacher,
            Subjects = subjects,
            Grades = grades,
            Groups = groups
        };

        return View("Assign", viewModel);
    }

    public async Task<IActionResult> GuardarAsignacion([FromBody] TeacherAssignmentRequest request)
    {
        if (request.GroupIds == null || !request.GroupIds.Any())
        {
            return BadRequest(new { success = false, message = "Debe seleccionar al menos un grupo." });
        }

        var insertedGroupIds = new List<Guid>();
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var schoolId = currentUser?.SchoolId;

        foreach (var groupId in request.GroupIds)
        {
            var subjectAssignmentId = await _academicAssignmentService.GetSubjectAssignmentIdAsync(
                request.SpecialtyId,
                request.AreaId,
                request.SubjectId,
                request.GradeId,
                groupId,
                schoolId
            );

            if (subjectAssignmentId == null)
                continue; // No se encontró la combinación completa

            var inserted = await _academicAssignmentService.AssignTeacherAsync(
                request.UserId,
                subjectAssignmentId.Value
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
                message = "Estas combinaciones ya existen o no fueron encontradas en las asignaciones académicas."
            });
        }

        var subject = await _subjectService.GetByIdAsync(request.SubjectId);
        var grade = await _gradeLevelService.GetByIdAsync(request.GradeId);
        var allGroups = await _groupService.GetAllAsync();

        var insertedGroupNames = allGroups
            .Where(g => insertedGroupIds.Contains(g.Id))
            .Select(g => g.Name)
            .ToList();

        var response = new
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
        };

        return Ok(response);
    }



    [HttpPost]
    public async Task<IActionResult> UpdateAssignments(Guid userId, List<Guid> subjectIds, List<Guid> groupIds, List<Guid> gradeLevelIds)
    {
        var user = await _userService.GetByIdWithRelationsAsync(userId);
        if (user == null) return NotFound();

        await _userService.UpdateAsync(user, subjectIds, groupIds, gradeLevelIds);

        return Json(new { success = true, message = "Asignaciones actualizadas correctamente." });
    }

    // Métodos CRUD para TeacherAssignment (los dejamos tal como están)
    public async Task<IActionResult> Details(Guid id)
    {
        var assignment = await _teacherAssignmentService.GetByIdAsync(id);
        if (assignment == null) return NotFound();
        return View(assignment);
    }

    public IActionResult Create() => View();

    //[HttpPost]
    //public async Task<IActionResult> Create(TeacherAssignment assignment)
    //{
    //    if (ModelState.IsValid)
    //    {
    //        await _teacherAssignmentService.CreateAsync(assignment);
    //        return RedirectToAction(nameof(Index));
    //    }
    //    return View(assignment);
    //}

    public async Task<IActionResult> Edit(Guid id)
    {
        var assignment = await _teacherAssignmentService.GetByIdAsync(id);
        if (assignment == null) return NotFound();
        return View(assignment);
    }

    //[HttpPost]
    //public async Task<IActionResult> Edit(TeacherAssignment assignment)
    //{
    //    if (ModelState.IsValid)
    //    {
    //        await _teacherAssignmentService.UpdateAsync(assignment);
    //        return RedirectToAction(nameof(Index));
    //    }
    //    return View(assignment);
    //}

    public async Task<IActionResult> Delete(Guid id)
    {
        var assignment = await _teacherAssignmentService.GetByIdAsync(id);
        if (assignment == null) return NotFound();
        return View(assignment);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        await _teacherAssignmentService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }


}
