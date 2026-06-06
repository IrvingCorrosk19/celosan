using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Application.Interfaces;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using SchoolManager.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManager.Controllers
{
    [Authorize(Roles = "admin,secretaria,director")]
    public class AcademicCatalogController : Controller
    {
        private readonly ISpecialtyService _specialtyService;
        private readonly IAreaService _areaService;
        private readonly ISubjectService _subjectService;
        private readonly IGradeLevelService _gradeLevelService;
        private readonly IGroupService _groupService;
        private readonly IShiftService _shiftService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITrimesterService _trimesterService;
        private readonly IAcademicAssignmentService _academicAssignmentService;
        private readonly SchoolDbContext _context;

        public AcademicCatalogController(
            ISpecialtyService specialtyService,
            IAreaService areaService,
            ISubjectService subjectService,
            IGradeLevelService gradeLevelService,
            IGroupService groupService,
            IShiftService shiftService,
            ICurrentUserService currentUserService,
            ITrimesterService trimesterService,
            IAcademicAssignmentService academicAssignmentService,
            SchoolDbContext context)
        {
            _specialtyService = specialtyService;
            _areaService = areaService;
            _subjectService = subjectService;
            _gradeLevelService = gradeLevelService;
            _groupService = groupService;
            _shiftService = shiftService;
            _currentUserService = currentUserService;
            _trimesterService = trimesterService;
            _academicAssignmentService = academicAssignmentService;
            _context = context;
        }

        private static string BuildCatalogCombinationKey(
            string especialidad, string area, string materia, string grado, string grupo)
            => $"{especialidad}|{area}|{materia}|{grado}|{grupo}";

        public async Task<IActionResult> Index()
        {
            var specialties = await _specialtyService.GetAllAsync();
            var areas = await _areaService.GetAllAsync();
            var subjects = await _subjectService.GetAllAsync();
            var grades = await _gradeLevelService.GetAllAsync();
            var groups = await _groupService.GetAllAsync();
            var trimestres = await _trimesterService.GetAllAsync();

            // Obtener jornadas de la tabla
            var shifts = await _shiftService.GetAllAsync();

            var viewModel = new AcademicCatalogViewModel
            {
                Specialties = specialties,
                Areas = areas,
                Subjects = subjects,
                GradesLevel = grades,
                Groups = groups,
                Trimestres = trimestres,
                Shifts = shifts
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Upload()
        {
            var ctx = await SchoolTenantHelper.TryGetBulkUploadSchoolContextAsync(_context, _currentUserService);
            ViewBag.BulkUploadSchoolName = ctx?.SchoolName;
            ViewBag.BulkUploadSchoolId = ctx?.SchoolId;
            ViewBag.BulkUploadBlocked = ctx == null;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SaveCatalog([FromBody] List<AcademicCatalogInputModel> catalogData)
        {
            if (catalogData == null || catalogData.Count == 0)
                return BadRequest(new { success = false, message = "No se recibieron datos del catálogo." });

            int asignacionesCreadas = 0;
            int duplicadasEnArchivo = 0;
            int duplicadasEnBd = 0;
            var errores = new List<string>();
            var combinacionesEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var currentUser = await _currentUserService.GetCurrentUserAsync();
            var schoolId = currentUser?.SchoolId;

            if (schoolId == null)
            {
                return BadRequest(new { success = false, message = "No se pudo obtener el ID de la escuela." });
            }

            for (var index = 0; index < catalogData.Count; index++)
            {
                var item = catalogData[index];
                var fila = index + 1;

                try
                {
                    var especialidad = string.IsNullOrWhiteSpace(item.Especialidad) ? "N/A" : item.Especialidad.Trim().ToUpper();
                    var area = string.IsNullOrWhiteSpace(item.Area) ? "N/A" : item.Area.Trim().ToUpper();
                    var materia = string.IsNullOrWhiteSpace(item.Materia) ? "N/A" : item.Materia.Trim().ToUpper();
                    var grado = string.IsNullOrWhiteSpace(item.Grado) ? "N/A" : item.Grado.Trim().ToUpper();
                    var grupo = string.IsNullOrWhiteSpace(item.Grupo) ? "N/A" : item.Grupo.Trim().ToUpper();
                    var combinacionKey = BuildCatalogCombinationKey(especialidad, area, materia, grado, grupo);

                    if (!combinacionesEnArchivo.Add(combinacionKey))
                    {
                        duplicadasEnArchivo++;
                        errores.Add($"Fila {fila}: Combinación duplicada en el archivo ({especialidad} / {area} / {materia} / {grado} / {grupo}).");
                        continue;
                    }

                    var specialty = await _specialtyService.GetOrCreateAsync(especialidad);
                    if (specialty == null)
                    {
                        errores.Add($"Fila {fila}: Error al crear/obtener especialidad: {especialidad}");
                        continue;
                    }

                    var areaEntity = await _areaService.GetOrCreateAsync(area);
                    if (areaEntity == null)
                    {
                        errores.Add($"Fila {fila}: Error al crear/obtener área: {area}");
                        continue;
                    }

                    var subject = await _subjectService.GetOrCreateAsync(materia);
                    if (subject == null)
                    {
                        errores.Add($"Fila {fila}: Error al crear/obtener materia: {materia}");
                        continue;
                    }

                    var grade = await _gradeLevelService.GetOrCreateAsync(grado);
                    if (grade == null)
                    {
                        errores.Add($"Fila {fila}: Error al crear/obtener grado: {grado}");
                        continue;
                    }

                    var groupEntity = await _groupService.GetOrCreateAsync(grupo);
                    if (groupEntity == null)
                    {
                        errores.Add($"Fila {fila}: Error al crear/obtener grupo: {grupo}");
                        continue;
                    }

                    var yaExiste = await _academicAssignmentService.ExisteAsignacionAsync(
                        specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id, schoolId);

                    if (yaExiste)
                    {
                        duplicadasEnBd++;
                        errores.Add($"Fila {fila}: La combinación ya existe en el sistema ({especialidad} / {area} / {materia} / {grado} / {grupo}).");
                        continue;
                    }

                    await _academicAssignmentService.CreateAsignacionAsync(
                        specialty.Id, areaEntity.Id, subject.Id, grade.Id, groupEntity.Id, schoolId);
                    asignacionesCreadas++;
                }
                catch (Exception ex)
                {
                    errores.Add($"Fila {fila}: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = true,
                asignacionesCreadas,
                duplicadasEnArchivo,
                duplicadasEnBd,
                errores,
                message = asignacionesCreadas > 0
                    ? $"Se crearon {asignacionesCreadas} asignaciones académicas nuevas."
                    : "No se crearon asignaciones nuevas."
            });
        }

        [HttpPost]
        public async Task<IActionResult> GuardarTrimestres([FromBody] List<TrimesterDto> trimestres)
        {
            try
            {
                if (trimestres == null || trimestres.Count == 0)
                {
                    return BadRequest(new { success = false, message = "No se recibieron datos de trimestres." });
                }

                await _trimesterService.GuardarTrimestresAsync(trimestres);
                return Ok(new { success = true, message = "Configuración de trimestres guardada correctamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActivarTrimestre([FromBody] TrimestreIdRequest request)
        {
            try
            {
                if (request == null || request.Id == Guid.Empty)
                {
                    return BadRequest(new { success = false, message = "ID de trimestre inválido." });
                }

                var resultado = await _trimesterService.ActivarTrimestreAsync(request.Id);
                if (resultado)
                {
                    return Ok(new { success = true, message = "Trimestre activado correctamente." });
                }
                else
                {
                    return NotFound(new { success = false, message = "Trimestre no encontrado." });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DesactivarTrimestre([FromBody] TrimestreIdRequest request)
        {
            try
            {
                if (request == null || request.Id == Guid.Empty)
                {
                    return BadRequest(new { success = false, message = "ID de trimestre inválido." });
                }

                var resultado = await _trimesterService.DesactivarTrimestreAsync(request.Id);
                if (resultado)
                {
                    return Ok(new { success = true, message = "Trimestre desactivado correctamente." });
                }
                else
                {
                    return NotFound(new { success = false, message = "Trimestre no encontrado." });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarTrimestre([FromBody] EditarTrimestreRequest request)
        {
            try
            {
                if (request == null || request.Id == Guid.Empty)
                {
                    return BadRequest(new { success = false, message = "ID de trimestre inválido." });
                }

                if (string.IsNullOrEmpty(request.StartDate) || string.IsNullOrEmpty(request.EndDate))
                {
                    return BadRequest(new { success = false, message = "Debes proporcionar ambas fechas." });
                }

                if (!DateTime.TryParse(request.StartDate, out var startDate) || 
                    !DateTime.TryParse(request.EndDate, out var endDate))
                {
                    return BadRequest(new { success = false, message = "Formato de fechas inválido." });
                }

                // Convertir fechas a UTC para consistencia
                startDate = DateTime.SpecifyKind(startDate, DateTimeKind.Unspecified).ToUniversalTime();
                endDate = DateTime.SpecifyKind(endDate, DateTimeKind.Unspecified).ToUniversalTime();

                var dto = new TrimesterDto
                {
                    Id = request.Id,
                    StartDate = startDate,
                    EndDate = endDate
                };

                var resultado = await _trimesterService.EditarFechasTrimestreAsync(dto);
                if (resultado)
                {
                    return Ok(new { success = true, message = "Fechas actualizadas correctamente." });
                }
                else
                {
                    return NotFound(new { success = false, message = "Trimestre no encontrado." });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarTodosLosTrimestres()
        {
            try
            {
                await _trimesterService.EliminarTodosLosTrimestresAsync();
                return Ok(new { success = true, message = "Todos los trimestres han sido eliminados." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // Gestión de Jornadas
        // ============================================

        [HttpPost]
        public async Task<IActionResult> CreateShift([FromBody] CreateShiftRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { success = false, message = "El nombre de la jornada es obligatorio." });
                }

                var shiftName = request.Name.Trim();
                
                // Validar que no exista ya
                var existingShift = await _shiftService.GetByNameAsync(shiftName);
                if (existingShift != null)
                {
                    return BadRequest(new { success = false, message = "La jornada ya existe en el sistema." });
                }

                var shift = new Shift
                {
                    Name = shiftName,
                    Description = request.Description?.Trim(),
                    IsActive = true,
                    DisplayOrder = request.DisplayOrder ?? 0
                };

                var createdShift = await _shiftService.CreateAsync(shift);
                return Ok(new { 
                    success = true, 
                    id = createdShift.Id,
                    name = createdShift.Name,
                    message = $"Jornada '{shiftName}' creada correctamente." 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateShift([FromBody] UpdateShiftRequest request)
        {
            try
            {
                if (request == null || request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { success = false, message = "Datos inválidos." });
                }

                var shift = await _shiftService.GetByIdAsync(request.Id);
                if (shift == null)
                {
                    return NotFound(new { success = false, message = "Jornada no encontrada." });
                }

                // Validar que el nuevo nombre no exista en otra jornada
                var existingShift = await _shiftService.GetByNameAsync(request.Name.Trim());
                if (existingShift != null && existingShift.Id != request.Id)
                {
                    return BadRequest(new { success = false, message = "Ya existe otra jornada con ese nombre." });
                }

                shift.Name = request.Name.Trim();
                shift.Description = request.Description?.Trim();
                shift.DisplayOrder = request.DisplayOrder ?? shift.DisplayOrder;
                shift.IsActive = request.IsActive ?? shift.IsActive;

                await _shiftService.UpdateAsync(shift);

                // Actualizar grupos que tienen esta jornada asignada
                var groups = await _groupService.GetAllAsync();
                var groupsToUpdate = groups.Where(g => g.ShiftId == request.Id).ToList();

                foreach (var group in groupsToUpdate)
                {
                    // Actualizar también el campo Shift por compatibilidad
                    group.Shift = shift.Name;
                    await _groupService.UpdateAsync(group);
                }

                return Ok(new { 
                    success = true, 
                    message = $"Jornada actualizada correctamente. {groupsToUpdate.Count} grupo(s) actualizado(s)." 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteShift([FromBody] DeleteShiftRequest request)
        {
            try
            {
                if (request == null || request.Id == Guid.Empty)
                {
                    return BadRequest(new { success = false, message = "Jornada inválida." });
                }

                var shift = await _shiftService.GetByIdAsync(request.Id);
                if (shift == null)
                {
                    return NotFound(new { success = false, message = "Jornada no encontrada." });
                }

                // Eliminar jornada de todos los grupos que la tienen
                var groups = await _groupService.GetAllAsync();
                var groupsToUpdate = groups.Where(g => g.ShiftId == request.Id).ToList();

                foreach (var group in groupsToUpdate)
                {
                    group.ShiftId = null;
                    group.Shift = null; // Por compatibilidad
                    await _groupService.UpdateAsync(group);
                }

                // Marcar jornada como inactiva (no se elimina físicamente)
                await _shiftService.DeleteAsync(request.Id);

                return Ok(new { 
                    success = true, 
                    message = $"Jornada eliminada correctamente. {groupsToUpdate.Count} grupo(s) actualizado(s)." 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetShiftGroupsCount()
        {
            try
            {
                var groups = await _groupService.GetAllAsync();
                var shifts = await _shiftService.GetAllAsync();
                
                var shiftCounts = shifts.Select(shift => new
                {
                    shiftId = shift.Id,
                    shift = shift.Name,
                    count = groups.Count(g => g.ShiftId == shift.Id)
                }).ToList();

                return Json(new { success = true, data = shiftCounts });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class CreateShiftRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }
    }

    public class UpdateShiftRequest
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    public class DeleteShiftRequest
    {
        public Guid Id { get; set; }
    }

    public class TrimestreIdRequest
    {
        public Guid Id { get; set; }
    }

    public class EditarTrimestreRequest
    {
        public Guid Id { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}