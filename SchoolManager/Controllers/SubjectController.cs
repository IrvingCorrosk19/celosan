using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;

[Authorize(Roles = "admin,secretaria,director")]
public class SubjectController : Controller
{
    private readonly ISubjectService _subjectService;

    public SubjectController(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    public async Task<IActionResult> Index()
    {
        var subjects = await _subjectService.GetAllAsync();
        return View(subjects);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var subject = await _subjectService.GetByIdAsync(id);
        if (subject == null) return NotFound();
        return View(subject);
    }

    //public IActionResult Create() => View();

    [HttpGet]
    public async Task<IActionResult> ListJson()
    {
        try
        {
            var subjects = await _subjectService.GetAllAsync();
            return Json(new { 
                success = true, 
                data = subjects.Select(s => new { 
                    id = s.Id, 
                    name = s.Name,
                    description = s.Description
                })
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al obtener las materias: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Subject subject)
    {
        if (string.IsNullOrWhiteSpace(subject.Name))
            return Json(new { success = false, message = "El nombre de la materia es obligatorio." });

        try
        {
            var created = await _subjectService.CreateAsync(subject);
            return Json(new { 
                success = true, 
                id = created.Id, 
                name = created.Name,
                description = created.Description,
                message = "Materia creada exitosamente."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al crear la materia: " + ex.Message });
        }
    }

    //public async Task<IActionResult> Edit(Guid id)
    //{
    //    var subject = await _subjectService.GetByIdAsync(id);
    //    if (subject == null) return NotFound();
    //    return View(subject);
    //}

    [HttpPost]
    public async Task<IActionResult> Edit([FromBody] Subject subject)
    {
        if (string.IsNullOrWhiteSpace(subject.Name))
            return Json(new { success = false, message = "El nombre de la materia es obligatorio." });

        try
        {
            var updated = await _subjectService.UpdateAsync(subject);
            return Json(new { 
                success = true, 
                id = updated.Id, 
                name = updated.Name,
                description = updated.Description,
                message = "Materia actualizada exitosamente."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al actualizar la materia: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive([FromBody] ToggleActiveSubjectRequest request)
    {
        if (request.Id == Guid.Empty)
            return Json(new { success = false, message = "ID de materia inválido." });

        try
        {
            var subject = await _subjectService.GetByIdAsync(request.Id);
            if (subject == null)
                return Json(new { success = false, message = "Materia no encontrada." });

            subject.Status = !(subject.Status ?? false);
            subject.UpdatedAt = DateTime.UtcNow;
            await _subjectService.UpdateAsync(subject);
            
            var estado = (subject.Status ?? false) ? "activada" : "desactivada";
            return Json(new { success = true, isActive = subject.Status, message = $"Materia {estado} exitosamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al cambiar el estado: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete([FromBody] DeleteSubjectRequest request)
    {
        if (request.Id == Guid.Empty)
            return Json(new { success = false, message = "ID de materia inválido." });

        try
        {
            var subject = await _subjectService.GetByIdAsync(request.Id);
            if (subject == null)
                return Json(new { success = false, message = "Materia no encontrada." });

            await _subjectService.DeleteAsync(request.Id);
            return Json(new { success = true, message = "Materia eliminada exitosamente." });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al eliminar la materia: " + ex.Message });
        }
    }
}

public class ToggleActiveSubjectRequest
{
    public Guid Id { get; set; }
}

public class DeleteSubjectRequest
{
    public Guid Id { get; set; }
}
