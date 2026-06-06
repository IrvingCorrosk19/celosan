using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,secretaria,director")]
[Route("GradeLevel")]
public class GradeLevelController : Controller
{
    private readonly IGradeLevelService _gradeLevelService;

    public GradeLevelController(IGradeLevelService gradeLevelService)
    {
        _gradeLevelService = gradeLevelService;
    }

    [HttpGet("ListJson")]
    public async Task<IActionResult> ListJson()
    {
        try
        {
            var result = await _gradeLevelService.GetAllAsync();
            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al obtener los grados: " + ex.Message });
        }
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] GradeLevel data)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(data.Name))
            {
                return Json(new { success = false, message = "El nombre del grado es obligatorio." });
            }

            data.Id = Guid.NewGuid();
            data.CreatedAt = DateTime.UtcNow;
            var created = await _gradeLevelService.CreateAsync(data);

            return Json(new
            {
                success = true,
                id = created.Id,
                name = created.Name,
                description = created.Description,
                message = "Grado creado exitosamente."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al crear el grado: " + ex.Message });
        }
    }

    [HttpPost("Edit")]
    public async Task<IActionResult> Edit([FromBody] GradeLevel data)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(data.Name))
            {
                return Json(new { success = false, message = "El nombre del grado es obligatorio." });
            }

            var updated = await _gradeLevelService.UpdateAsync(data);
            return Json(new { 
                success = true, 
                id = updated.Id, 
                name = updated.Name, 
                description = updated.Description,
                message = "Grado actualizado exitosamente."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al actualizar el grado: " + ex.Message });
        }
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteGradeRequest request)
    {
        try
        {
            if (request.id == Guid.Empty)
            {
                return Json(new { success = false, message = "ID de grado inválido." });
            }

            var result = await _gradeLevelService.DeleteAsync(request.id);
            if (!result)
            {
                return Json(new { success = false, message = "No se encontró el grado a eliminar." });
            }

            return Json(new { success = true, message = "Grado eliminado exitosamente." });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error al eliminar el grado: " + ex.Message });
        }
    }
}

public class DeleteGradeRequest
{
    public Guid id { get; set; }
}
