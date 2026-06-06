using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Models.ViewModels;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManager.Controllers
{
    public class SubjectAssignmentController : Controller
    {
        private readonly SchoolDbContext _context;
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ISubjectService _subjectService;
        private readonly IGroupService _groupService;
        private readonly IGradeLevelService _gradeLevelService;
        private readonly IAreaService _areaService;
        private readonly ISpecialtyService _specialtyService;
        private readonly IStudentAssignmentService _studentAssignmentService;
        private readonly ISubjectAssignmentService _subjectAssignmentService;
        private readonly IShiftService _shiftService;


        public SubjectAssignmentController(
            SchoolDbContext context,
            IUserService userService,
            ICurrentUserService currentUserService,
            ISubjectService subjectService,
            IGroupService groupService,
            IGradeLevelService gradeLevelService,
            IAreaService areaService,
            ISpecialtyService specialtyService,
            IStudentAssignmentService studentAssignmentService,
            ISubjectAssignmentService subjectAssignmentService,
            IShiftService shiftService)
        {
            _context = context;
            _userService = userService;
            _currentUserService = currentUserService;
            _subjectService = subjectService;
            _groupService = groupService;
            _gradeLevelService = gradeLevelService;
            _areaService = areaService;
            _specialtyService = specialtyService;
            _studentAssignmentService = studentAssignmentService;
            _subjectAssignmentService = subjectAssignmentService;
            _shiftService = shiftService;

        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Obtener el usuario actual y su escuela
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var schoolId = currentUser.SchoolId;
            if (schoolId == null)
            {
                return View(new List<SubjectAssignmentViewModel>());
            }

            // Obtener solo las asignaciones de la escuela del usuario
            var subjectAssignments = await _context.SubjectAssignments
                .Include(sa => sa.Specialty)
                .Include(sa => sa.Area)
                .Include(sa => sa.Subject)
                .Include(sa => sa.GradeLevel)
                .Include(sa => sa.Group)
                .Where(sa => sa.SchoolId == schoolId)
                .ToListAsync();

            var viewModel = subjectAssignments.Select(sa => new SubjectAssignmentViewModel
            {
                Id = sa.Id,
                SpecialtyId = sa.SpecialtyId,
                AreaId = sa.AreaId,
                SubjectId = sa.SubjectId,
                GradeLevelId = sa.GradeLevelId,
                GroupId = sa.GroupId,

                SpecialtyName = sa.Specialty.Name,
                AreaName = sa.Area.Name,
                SubjectName = sa.Subject.Name,
                GradeLevelName = sa.GradeLevel.Name,
                GroupName = sa.Group.Name,

                // Agregar el campo Status
                Status = sa.Status ?? "Active" // Asignar 'Active' si Status es null
            }).ToList();

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAssignments()
        {
            // Obtener el usuario actual y su escuela
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Usuario no autenticado." });
            }

            var schoolId = currentUser.SchoolId;
            if (schoolId == null)
            {
                return Json(new { success = true, assignments = new List<object>() });
            }

            var allAssignments = await _context.SubjectAssignments
                .Include(sa => sa.Specialty)
                .Include(sa => sa.Area)
                .Include(sa => sa.Subject)
                .Include(sa => sa.GradeLevel)
                .Include(sa => sa.Group)
                .Where(sa => sa.SchoolId == schoolId)
                .Select(sa => new
                {
                    sa.Id,
                    sa.SpecialtyId,
                    sa.AreaId,
                    sa.SubjectId,
                    sa.GradeLevelId,
                    sa.GroupId,
                    SpecialtyName = sa.Specialty.Name,
                    AreaName = sa.Area.Name,
                    SubjectName = sa.Subject.Name,
                    GradeLevelName = sa.GradeLevel.Name,
                    GroupName = sa.Group.Name
                })
                .ToListAsync();

            return Json(new { success = true, assignments = allAssignments });
        }

        [HttpGet]
        public async Task<IActionResult> GetDropdownData()
        {
            try
            {
                // Obtener el usuario actual y su escuela
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado." });
                }

                var schoolId = currentUser.SchoolId;
                if (schoolId == null)
                {
                    return Json(new { success = false, message = "Usuario no tiene escuela asignada." });
                }

                // Obtener todos los datos disponibles (sin filtrar por SchoolId ya que algunos modelos no lo tienen)
                var specialties = await _context.Specialties.ToListAsync();
                var areas = await _context.Areas.ToListAsync();
                var subjects = await _context.Subjects.ToListAsync();
                var gradeLevels = await _context.GradeLevels.ToListAsync();
                var groups = await _context.Groups.ToListAsync();

                return Json(new
                {
                    success = true,
                    specialties = specialties.Select(s => new { s.Id, s.Name }),
                    areas = areas.Select(a => new { a.Id, a.Name }),
                    subjects = subjects.Select(s => new { s.Id, s.Name }),
                    gradeLevels = gradeLevels.Select(g => new { g.Id, g.Name }),
                    groups = groups.Select(g => new { g.Id, g.Name })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al cargar los datos: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SubjectAssignmentCreateDto model)
        {
            // Validaciones básicas del modelo
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "Los datos no son válidos.", errors = errors });
            }

            // Validaciones de campos requeridos
            if (model.SpecialtyId == Guid.Empty)
                return Json(new { success = false, message = "La especialidad es requerida." });
            
            if (model.AreaId == Guid.Empty)
                return Json(new { success = false, message = "El área es requerida." });
            
            if (model.SubjectId == Guid.Empty)
                return Json(new { success = false, message = "La materia es requerida." });
            
            if (model.GradeLevelId == Guid.Empty)
                return Json(new { success = false, message = "El grado es requerido." });
            
            if (model.GroupId == Guid.Empty)
                return Json(new { success = false, message = "El grupo es requerido." });

            try
            {
                // Obtener el usuario actual y su escuela
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado." });
                }

                var schoolId = currentUser.SchoolId;
                if (schoolId == null)
                {
                    return Json(new { success = false, message = "Usuario no tiene escuela asignada." });
                }

                // Validar que los IDs existan en la base de datos
                var specialtyExists = await _context.Specialties.AnyAsync(s => s.Id == model.SpecialtyId);
                if (!specialtyExists)
                    return Json(new { success = false, message = "La especialidad seleccionada no existe." });

                var areaExists = await _context.Areas.AnyAsync(a => a.Id == model.AreaId);
                if (!areaExists)
                    return Json(new { success = false, message = "El área seleccionada no existe." });

                var subjectExists = await _context.Subjects.AnyAsync(s => s.Id == model.SubjectId);
                if (!subjectExists)
                    return Json(new { success = false, message = "La materia seleccionada no existe." });

                var gradeLevelExists = await _context.GradeLevels.AnyAsync(g => g.Id == model.GradeLevelId);
                if (!gradeLevelExists)
                    return Json(new { success = false, message = "El grado seleccionado no existe." });

                var groupExists = await _context.Groups.AnyAsync(g => g.Id == model.GroupId);
                if (!groupExists)
                    return Json(new { success = false, message = "El grupo seleccionado no existe." });

                // Obtener los nombres de los elementos para mensajes más descriptivos
                var specialty = await _context.Specialties.FindAsync(model.SpecialtyId);
                var area = await _context.Areas.FindAsync(model.AreaId);
                var subject = await _context.Subjects.FindAsync(model.SubjectId);
                var gradeLevel = await _context.GradeLevels.FindAsync(model.GradeLevelId);
                var group = await _context.Groups.FindAsync(model.GroupId);

                // Verificar que no exista otra asignación con la misma combinación completa
                var existingAssignment = await _context.SubjectAssignments
                    .Include(sa => sa.Specialty)
                    .Include(sa => sa.Area)
                    .Include(sa => sa.Subject)
                    .Include(sa => sa.GradeLevel)
                    .Include(sa => sa.Group)
                    .FirstOrDefaultAsync(sa =>
                        sa.SpecialtyId == model.SpecialtyId &&
                        sa.AreaId == model.AreaId &&
                        sa.SubjectId == model.SubjectId &&
                        sa.GradeLevelId == model.GradeLevelId &&
                        sa.GroupId == model.GroupId &&
                        sa.SchoolId == schoolId
                    );

                if (existingAssignment != null)
                {
                    var message = $"Ya existe una asignación con la siguiente combinación:\n" +
                                $"• Especialidad: {existingAssignment.Specialty.Name}\n" +
                                $"• Área: {existingAssignment.Area.Name}\n" +
                                $"• Materia: {existingAssignment.Subject.Name}\n" +
                                $"• Grado: {existingAssignment.GradeLevel.Name}\n" +
                                $"• Grupo: {existingAssignment.Group.Name}\n\n" +
                                $"Esta combinación ya está registrada en el sistema.";

                    return Json(new { success = false, message = message });
                }

                // Verificar combinaciones parciales que podrían causar conflictos
                var sameSpecialtyAreaSubject = await _context.SubjectAssignments
                    .Include(sa => sa.Specialty)
                    .Include(sa => sa.Area)
                    .Include(sa => sa.Subject)
                    .Include(sa => sa.GradeLevel)
                    .Include(sa => sa.Group)
                    .FirstOrDefaultAsync(sa =>
                        sa.SpecialtyId == model.SpecialtyId &&
                        sa.AreaId == model.AreaId &&
                        sa.SubjectId == model.SubjectId &&
                        sa.SchoolId == schoolId &&
                        (sa.GradeLevelId != model.GradeLevelId || sa.GroupId != model.GroupId)
                    );

                if (sameSpecialtyAreaSubject != null)
                {
                    var message = $"Ya existe una asignación con la misma Especialidad, Área y Materia:\n" +
                                $"• Especialidad: {sameSpecialtyAreaSubject.Specialty.Name}\n" +
                                $"• Área: {sameSpecialtyAreaSubject.Area.Name}\n" +
                                $"• Materia: {sameSpecialtyAreaSubject.Subject.Name}\n" +
                                $"• Grado: {sameSpecialtyAreaSubject.GradeLevel.Name}\n" +
                                $"• Grupo: {sameSpecialtyAreaSubject.Group.Name}\n\n" +
                                $"Verifique que no esté duplicando la misma materia para diferentes grados o grupos.";

                    return Json(new { success = false, message = message });
                }

                // Verificar si la materia ya está asignada al mismo grupo
                var sameSubjectGroup = await _context.SubjectAssignments
                    .Include(sa => sa.Specialty)
                    .Include(sa => sa.Area)
                    .Include(sa => sa.Subject)
                    .Include(sa => sa.GradeLevel)
                    .Include(sa => sa.Group)
                    .FirstOrDefaultAsync(sa =>
                        sa.SubjectId == model.SubjectId &&
                        sa.GroupId == model.GroupId &&
                        sa.SchoolId == schoolId &&
                        sa.Id != Guid.Empty // Excluir la asignación actual si estamos editando
                    );

                if (sameSubjectGroup != null)
                {
                    var message = $"La materia '{subject?.Name}' ya está asignada al grupo '{group?.Name}' con:\n" +
                                $"• Especialidad: {sameSubjectGroup.Specialty.Name}\n" +
                                $"• Área: {sameSubjectGroup.Area.Name}\n" +
                                $"• Grado: {sameSubjectGroup.GradeLevel.Name}\n\n" +
                                $"Una materia no puede estar asignada al mismo grupo más de una vez.";

                    return Json(new { success = false, message = message });
                }

                var subjectAssignment = new SubjectAssignment
                {
                    Id = Guid.NewGuid(),
                    SpecialtyId = model.SpecialtyId,
                    AreaId = model.AreaId,
                    SubjectId = model.SubjectId,
                    GradeLevelId = model.GradeLevelId,
                    GroupId = model.GroupId,
                    SchoolId = schoolId,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };

                _context.SubjectAssignments.Add(subjectAssignment);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Asignación creada correctamente." });
            }
            catch (DbUpdateException ex)
            {
                return Json(new { success = false, message = "Error al crear la asignación en la base de datos. Por favor revisa los datos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Ocurrió un error inesperado: " + ex.Message });
            }
        }



        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] EditSubjectAssignmentViewModel model)
        {
            // Validaciones básicas del modelo
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "Los datos no son válidos.", errors = errors });
            }

            // Validaciones de campos requeridos
            if (model.Id == Guid.Empty)
                return Json(new { success = false, message = "El ID de la asignación es requerido." });
            
            if (model.SpecialtyId == Guid.Empty)
                return Json(new { success = false, message = "La especialidad es requerida." });
            
            if (model.AreaId == Guid.Empty)
                return Json(new { success = false, message = "El área es requerida." });
            
            if (model.SubjectId == Guid.Empty)
                return Json(new { success = false, message = "La materia es requerida." });
            
            if (model.GradeLevelId == Guid.Empty)
                return Json(new { success = false, message = "El grado es requerido." });
            
            if (model.GroupId == Guid.Empty)
                return Json(new { success = false, message = "El grupo es requerido." });

            try
            {
                var subjectAssignment = await _context.SubjectAssignments
                    .FirstOrDefaultAsync(sa => sa.Id == model.Id);

                if (subjectAssignment == null)
                {
                    return Json(new { success = false, message = "La asignación no existe." });
                }

                // Validar que los IDs existan en la base de datos
                var specialtyExists = await _context.Specialties.AnyAsync(s => s.Id == model.SpecialtyId);
                if (!specialtyExists)
                    return Json(new { success = false, message = "La especialidad seleccionada no existe." });

                var areaExists = await _context.Areas.AnyAsync(a => a.Id == model.AreaId);
                if (!areaExists)
                    return Json(new { success = false, message = "El área seleccionada no existe." });

                var subjectExists = await _context.Subjects.AnyAsync(s => s.Id == model.SubjectId);
                if (!subjectExists)
                    return Json(new { success = false, message = "La materia seleccionada no existe." });

                var gradeLevelExists = await _context.GradeLevels.AnyAsync(g => g.Id == model.GradeLevelId);
                if (!gradeLevelExists)
                    return Json(new { success = false, message = "El grado seleccionado no existe." });

                var groupExists = await _context.Groups.AnyAsync(g => g.Id == model.GroupId);
                if (!groupExists)
                    return Json(new { success = false, message = "El grupo seleccionado no existe." });

                // Obtener los nombres de los elementos para mensajes más descriptivos
                var specialty = await _context.Specialties.FindAsync(model.SpecialtyId);
                var area = await _context.Areas.FindAsync(model.AreaId);
                var subject = await _context.Subjects.FindAsync(model.SubjectId);
                var gradeLevel = await _context.GradeLevels.FindAsync(model.GradeLevelId);
                var group = await _context.Groups.FindAsync(model.GroupId);

                // Verificar que no exista otra asignación con la misma combinación completa
                var existingAssignment = await _context.SubjectAssignments
                    .Include(sa => sa.Specialty)
                    .Include(sa => sa.Area)
                    .Include(sa => sa.Subject)
                    .Include(sa => sa.GradeLevel)
                    .Include(sa => sa.Group)
                    .FirstOrDefaultAsync(sa =>
                        sa.SpecialtyId == model.SpecialtyId &&
                        sa.AreaId == model.AreaId &&
                        sa.SubjectId == model.SubjectId &&
                        sa.GradeLevelId == model.GradeLevelId &&
                        sa.GroupId == model.GroupId &&
                        sa.Id != model.Id
                    );

                if (existingAssignment != null)
                {
                    var message = $"Ya existe una asignación con la siguiente combinación:\n" +
                                $"• Especialidad: {existingAssignment.Specialty.Name}\n" +
                                $"• Área: {existingAssignment.Area.Name}\n" +
                                $"• Materia: {existingAssignment.Subject.Name}\n" +
                                $"• Grado: {existingAssignment.GradeLevel.Name}\n" +
                                $"• Grupo: {existingAssignment.Group.Name}\n\n" +
                                $"Esta combinación ya está registrada en el sistema.";

                    return Json(new { success = false, message = message });
                }

                // Verificar combinaciones parciales que podrían causar conflictos
                var sameSpecialtyAreaSubject = await _context.SubjectAssignments
                    .Include(sa => sa.Specialty)
                    .Include(sa => sa.Area)
                    .Include(sa => sa.Subject)
                    .Include(sa => sa.GradeLevel)
                    .Include(sa => sa.Group)
                    .FirstOrDefaultAsync(sa =>
                        sa.SpecialtyId == model.SpecialtyId &&
                        sa.AreaId == model.AreaId &&
                        sa.SubjectId == model.SubjectId &&
                        sa.Id != model.Id &&
                        (sa.GradeLevelId != model.GradeLevelId || sa.GroupId != model.GroupId)
                    );

                if (sameSpecialtyAreaSubject != null)
                {
                    var message = $"Ya existe una asignación con la misma Especialidad, Área y Materia:\n" +
                                $"• Especialidad: {sameSpecialtyAreaSubject.Specialty.Name}\n" +
                                $"• Área: {sameSpecialtyAreaSubject.Area.Name}\n" +
                                $"• Materia: {sameSpecialtyAreaSubject.Subject.Name}\n" +
                                $"• Grado: {sameSpecialtyAreaSubject.GradeLevel.Name}\n" +
                                $"• Grupo: {sameSpecialtyAreaSubject.Group.Name}\n\n" +
                                $"Verifique que no esté duplicando la misma materia para diferentes grados o grupos.";

                    return Json(new { success = false, message = message });
                }

                // Verificar si la materia ya está asignada al mismo grupo
                var sameSubjectGroup = await _context.SubjectAssignments
                    .Include(sa => sa.Specialty)
                    .Include(sa => sa.Area)
                    .Include(sa => sa.Subject)
                    .Include(sa => sa.GradeLevel)
                    .Include(sa => sa.Group)
                    .FirstOrDefaultAsync(sa =>
                        sa.SubjectId == model.SubjectId &&
                        sa.GroupId == model.GroupId &&
                        sa.Id != model.Id
                    );

                if (sameSubjectGroup != null)
                {
                    var message = $"La materia '{subject?.Name}' ya está asignada al grupo '{group?.Name}' con:\n" +
                                $"• Especialidad: {sameSubjectGroup.Specialty.Name}\n" +
                                $"• Área: {sameSubjectGroup.Area.Name}\n" +
                                $"• Grado: {sameSubjectGroup.GradeLevel.Name}\n\n" +
                                $"Una materia no puede estar asignada al mismo grupo más de una vez.";

                    return Json(new { success = false, message = message });
                }

                // Actualizar la asignación
                subjectAssignment.SpecialtyId = model.SpecialtyId;
                subjectAssignment.AreaId = model.AreaId;
                subjectAssignment.SubjectId = model.SubjectId;
                subjectAssignment.GradeLevelId = model.GradeLevelId;
                subjectAssignment.GroupId = model.GroupId;

                _context.SubjectAssignments.Update(subjectAssignment);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Asignación actualizada correctamente." });
            }
            catch (DbUpdateException ex)
            {
                // Esto atrapa errores de base de datos como llaves duplicadas, constraints, etc.
                return Json(new { success = false, message = "Error al actualizar la asignación en la base de datos. Por favor revisa los datos." });
            }
            catch (Exception ex)
            {
                // Cualquier otro error inesperado
                return Json(new { success = false, message = "Ocurrió un error inesperado: " + ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> Delete(Guid id)
        {
            // Validar que el ID no esté vacío
            if (id == Guid.Empty)
            {
                TempData["ErrorMessage"] = "ID de asignación inválido.";
                return RedirectToAction("Index");
            }

            try
            {
                var subjectAssignment = await _context.SubjectAssignments.FindAsync(id);
                if (subjectAssignment == null)
                {
                    TempData["ErrorMessage"] = "No se encontró la asignación especificada.";
                    return RedirectToAction("Index");
                }

                // Verificar si hay dependencias (estudiantes asignados, calificaciones, etc.)
                var hasStudentAssignments = await _context.StudentAssignments
                    .AnyAsync(sa => sa.GradeId == subjectAssignment.GradeLevelId && sa.GroupId == subjectAssignment.GroupId);
                
                if (hasStudentAssignments)
                {
                    TempData["ErrorMessage"] = "No se puede eliminar la asignación porque tiene estudiantes asignados.";
                    return RedirectToAction("Index");
                }

                _context.SubjectAssignments.Remove(subjectAssignment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Asignación eliminada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al eliminar la asignación: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAssignment([FromBody] Guid id)
        {
            // Validar que el ID no esté vacío
            if (id == Guid.Empty)
                return Json(new { success = false, message = "ID de asignación inválido." });

            try
            {
                var subjectAssignment = await _context.SubjectAssignments.FindAsync(id);
                if (subjectAssignment == null)
                    return Json(new { success = false, message = "No se encontró la asignación." });

                // Verificar si hay dependencias (estudiantes asignados, calificaciones, etc.)
                var hasStudentAssignments = await _context.StudentAssignments
                    .AnyAsync(sa => sa.GradeId == subjectAssignment.GradeLevelId && sa.GroupId == subjectAssignment.GroupId);
                
                if (hasStudentAssignments)
                {
                    return Json(new { success = false, message = "No se puede eliminar la asignación porque tiene estudiantes asignados." });
                }

                _context.SubjectAssignments.Remove(subjectAssignment);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Asignación eliminada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar la asignación: {ex.Message}" });
            }
        }

        // Método para carga masiva
        [HttpPost]
        public async Task<IActionResult> SaveAssignments([FromBody] List<StudentAssignmentInputModel> asignaciones)
        {
            // Validaciones básicas
            if (asignaciones == null || asignaciones.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron asignaciones." });

            if (asignaciones.Count > 1000)
                return BadRequest(new { success = false, message = "No se pueden procesar más de 1000 asignaciones a la vez." });

            int insertadas = 0;
            int duplicadas = 0;
            int erroresValidacion = 0;
            var errores = new List<string>();

            foreach (var item in asignaciones)
            {
                try
                {
                    // Validaciones de campos requeridos
                    if (string.IsNullOrWhiteSpace(item.Estudiante))
                    {
                        errores.Add($"Email del estudiante es requerido en la fila {asignaciones.IndexOf(item) + 1}");
                        erroresValidacion++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.Grado))
                    {
                        errores.Add($"Grado es requerido para {item.Estudiante}");
                        erroresValidacion++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.Grupo))
                    {
                        errores.Add($"Grupo es requerido para {item.Estudiante}");
                        erroresValidacion++;
                        continue;
                    }

                    // Validar formato de email
                    if (!IsValidEmail(item.Estudiante))
                    {
                        errores.Add($"Email inválido: {item.Estudiante}");
                        erroresValidacion++;
                        continue;
                    }

                    // Validar campos adicionales si están presentes
                    if (!string.IsNullOrWhiteSpace(item.Nombre) && item.Nombre.Length > 100)
                    {
                        errores.Add($"Nombre muy largo para {item.Estudiante} (máximo 100 caracteres)");
                        erroresValidacion++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(item.Apellido) && item.Apellido.Length > 100)
                    {
                        errores.Add($"Apellido muy largo para {item.Estudiante} (máximo 100 caracteres)");
                        erroresValidacion++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(item.DocumentoId) && item.DocumentoId.Length > 20)
                    {
                        errores.Add($"Documento ID muy largo para {item.Estudiante} (máximo 20 caracteres)");
                        erroresValidacion++;
                        continue;
                    }

                    // Validar fecha de nacimiento si está presente
                    if (!string.IsNullOrWhiteSpace(item.FechaNacimiento))
                    {
                        if (!DateTime.TryParseExact(item.FechaNacimiento, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime fechaNac))
                        {
                            errores.Add($"Fecha de nacimiento inválida para {item.Estudiante}. Use formato DD/MM/YYYY");
                            erroresValidacion++;
                            continue;
                        }

                        // Especificar que es fecha local y convertir a UTC para consistencia
                        fechaNac = DateTime.SpecifyKind(fechaNac, DateTimeKind.Unspecified).ToUniversalTime();

                        // Validar que la fecha no sea futura (usar UTC para consistencia)
                        var fechaActual = DateTime.UtcNow;
                        if (fechaNac > fechaActual)
                        {
                            errores.Add($"Fecha de nacimiento no puede ser futura para {item.Estudiante}");
                            erroresValidacion++;
                            continue;
                        }

                        // Validar que la edad sea razonable (entre 5 y 25 años)
                        // Calcular edad correctamente considerando mes y día
                        var edad = fechaActual.Year - fechaNac.Year;
                        if (fechaNac.Date > fechaActual.Date.AddYears(-edad)) edad--;
                        if (edad < 5 || edad > 25)
                        {
                            errores.Add($"Edad no válida para {item.Estudiante} ({edad} años). Debe estar entre 5 y 25 años");
                            erroresValidacion++;
                            continue;
                        }
                    }

                    var currentSchoolId = (await _currentUserService.GetCurrentUserAsync())?.SchoolId;
                    const string jornadaNocturna = "Noche";
                    var shift = await _shiftService.GetOrCreateAsync(jornadaNocturna);

                    var student = await _userService.GetByEmailAsync(item.Estudiante);
                    var grade = await _gradeLevelService.GetByNameAsync(item.Grado);
                    var group = await _groupService.GetByNameAndGradeAsync(item.Grupo, currentSchoolId, shift.Id);

                    if (group != null && (group.ShiftId == null || group.ShiftId != shift.Id))
                    {
                        group.ShiftId = shift.Id;
                        group.Shift = shift.Name;
                        group.UpdatedAt = DateTime.UtcNow;
                        await _groupService.UpdateAsync(group);
                    }

                    if (student == null || grade == null || group == null)
                    {
                        errores.Add($"Error de datos: {item.Estudiante} - {item.Grado} - {item.Grupo}");
                        continue;
                    }

                    bool exists = await _studentAssignmentService.ExistsWithShiftAsync(student.Id, grade.Id, group.Id, shift.Id);
                    if (exists)
                    {
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

                    await _studentAssignmentService.InsertAsync(assignment);
                    insertadas++;
                }
                catch (Exception ex)
                {
                    errores.Add($"Excepción en {item.Estudiante}: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = true,
                insertadas,
                duplicadas,
                erroresValidacion,
                errores,
                message = $"Carga masiva completada. Insertadas: {insertadas}, Duplicadas: {duplicadas}, Errores de validación: {erroresValidacion}"
            });
        }

        // Método para asignaciones individuales
        [HttpPost]
        public async Task<IActionResult> SaveAssignmentsSingle([FromBody] List<SubjectAssignmentPreview> asignaciones)
        {
            if (asignaciones == null || asignaciones.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron asignaciones." });

            var asignacionesCreadas = new List<string>();

            foreach (var item in asignaciones)
            {
                var materia = await _context.Subjects.FirstOrDefaultAsync(s => s.Name.ToLower() == item.Materia.ToLower());
                var grado = await _context.GradeLevels.FirstOrDefaultAsync(g => g.Name.ToLower() == item.Grado.ToLower());
                var grupo = await _context.Groups.FirstOrDefaultAsync(g => g.Name.ToLower() == item.Grupo.ToLower());

                if (materia != null && grado != null && grupo != null)
                {
                    bool yaExiste = await _context.SubjectAssignments.AnyAsync(a =>
                        a.SubjectId == materia.Id &&
                        a.GroupId == grupo.Id);

                    if (!yaExiste)
                    {
                        _context.SubjectAssignments.Add(new SubjectAssignment
                        {
                            Id = Guid.NewGuid(),
                            SubjectId = materia.Id,
                            GroupId = grupo.Id,
                            CreatedAt = DateTime.UtcNow
                        });

                        asignacionesCreadas.Add($"{materia.Name} - {grado.Name} - {grupo.Name}");
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"{asignacionesCreadas.Count} asignaciones guardadas.",
                detalles = asignacionesCreadas
            });
        }

        [HttpGet]
        public async Task<IActionResult> ChangeStatus(Guid id)
        {
            // Validar que el ID no esté vacío
            if (id == Guid.Empty)
            {
                TempData["ErrorMessage"] = "ID de asignación inválido.";
                return RedirectToAction("Index");
            }

            try
            {
                var item = await _context.SubjectAssignments.FindAsync(id);
                if (item == null)
                {
                    TempData["ErrorMessage"] = "No se encontró la asignación especificada.";
                    return RedirectToAction("Index");
                }

                // Cambiar el estado (asegurándose de que sea un valor válido)
                if (item.Status == "Active")
                {
                    item.Status = "Inactive";
                }
                else if (item.Status == "Inactive")
                {
                    item.Status = "Active";
                }
                else
                {
                    // Si el estado no es válido, establecer un estado por defecto
                    item.Status = "Active";
                }

                // Guardar los cambios en la base de datos
                _context.Update(item);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = $"Estado de la asignación cambiado a {item.Status}.";
            }
            catch (DbUpdateException ex)
            {
                // Manejar el error de la base de datos (si lo hay)
                var innerException = ex.InnerException?.Message;
                TempData["ErrorMessage"] = $"Error al actualizar el estado: {innerException}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error inesperado: {ex.Message}";
            }

            // Redirigir de vuelta a la vista
            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para validar formato de email
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

    }
}
