using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,superadmin,secretaria")]
public class CurriculumLoadController : Controller
{
    private readonly ICurriculumLoadService _curriculumLoadService;
    private readonly ICurrentUserService _currentUserService;

    public CurriculumLoadController(
        ICurriculumLoadService curriculumLoadService,
        ICurrentUserService currentUserService)
    {
        _curriculumLoadService = curriculumLoadService;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (!CurriculumLoadAccess.CanView(user))
            return Forbid();

        ViewBag.CanEdit = CurriculumLoadAccess.CanEdit(user);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetAcademicPrograms()
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (!CurriculumLoadAccess.CanView(user))
            return Forbid();

        var programs = await _curriculumLoadService.GetProgramsForAdministrationAsync();
        return Json(new { success = true, data = programs, canEdit = CurriculumLoadAccess.CanEdit(user) });
    }

    [HttpGet]
    public async Task<IActionResult> GetCargaHoraria(Guid programId, string? viewMode = null, int? selectedGrade = null)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (!CurriculumLoadAccess.CanView(user))
            return Forbid();

        var report = await _curriculumLoadService.GetReportAsync(
            programId,
            viewMode ?? CurriculumLoadLevel.ViewFullProgram,
            selectedGrade,
            includeInactive: false);

        if (report == null)
            return Json(new { success = false, message = "No hay estructura curricular para este programa." });

        report.CanEdit = CurriculumLoadAccess.CanEdit(user);
        report.InactiveSubjects = await _curriculumLoadService.GetInactiveSubjectsAsync(programId);
        return Json(new { success = true, data = report });
    }

    [HttpPost]
    public async Task<IActionResult> SaveHours([FromBody] CurriculumLoadSaveHoursRequest request)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (!CurriculumLoadAccess.CanEdit(user))
            return StatusCode(403, new { success = false, message = "No tiene permiso para editar la carga horaria." });

        if (request.CurriculumLoadSubjectId == Guid.Empty)
            return BadRequest(new { success = false, message = "Asignatura curricular requerida." });

        try
        {
            await _curriculumLoadService.SaveHoursAsync(
                request.CurriculumLoadSubjectId, request.B1, request.B2, request.OnlyBlock);
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetActive([FromBody] CurriculumLoadSetActiveRequest request)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (!CurriculumLoadAccess.CanEdit(user))
            return StatusCode(403, new { success = false, message = "No tiene permiso para administrar la carga horaria." });

        if (request.CurriculumLoadSubjectId == Guid.Empty)
            return BadRequest(new { success = false, message = "Asignatura curricular requerida." });

        try
        {
            await _curriculumLoadService.SetActiveAsync(request.CurriculumLoadSubjectId, request.IsActive);
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
