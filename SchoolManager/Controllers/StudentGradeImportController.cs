using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Dtos;
using SchoolManager.Services.Implementations;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,secretaria,superadmin,Admin,Secretaria,SuperAdmin")]
public class StudentGradeImportController : Controller
{
    public const long MaxUploadBytes = StudentGradeImportExcelParser.MaxFileBytes;

    private readonly IStudentGradeImportService _importService;
    private readonly ICurrentUserService _currentUserService;

    public StudentGradeImportController(
        IStudentGradeImportService importService,
        ICurrentUserService currentUserService)
    {
        _importService = importService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var bytes = StudentGradeImportTemplateBuilder.Build();
        return File(bytes, StudentGradeImportTemplateBuilder.ContentType, StudentGradeImportTemplateBuilder.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        var page = await _importService.GetPageAsync(school?.Id);
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Analyze(Guid academicYearId, IFormFile? file)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (user == null)
        {
            TempData["Error"] = "No se pudo identificar al usuario autenticado.";
            return RedirectToAction(nameof(Index));
        }

        if (school == null)
        {
            TempData["Error"] = "Debe existir una escuela concreta en el usuario autenticado. No se acepta SchoolId del formulario ni del Excel.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _importService.AnalyzeAsync(user.Id, school.Id, academicYearId, file!);
        if (result.FileError)
        {
            TempData["Error"] = result.FileErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        return View("Preview", result.Preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string previewToken)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        var school = await _currentUserService.GetCurrentUserSchoolAsync();
        if (user == null)
        {
            TempData["Error"] = "No se pudo identificar al usuario autenticado.";
            return RedirectToAction(nameof(Index));
        }

        if (school == null)
        {
            TempData["Error"] = "Debe existir una escuela concreta en el usuario autenticado. No se acepta SchoolId del formulario ni del Excel.";
            return RedirectToAction(nameof(Index));
        }

        var userName = $"{user.Name} {user.LastName}".Trim();
        var result = await _importService.ConfirmAsync(user.Id, school.Id, previewToken, userName, user.Role);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? GradeImportConfirmMessages.TokenInvalid;
            return RedirectToAction(nameof(Index));
        }

        return View("Result", result.Summary);
    }
}
