using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,superadmin,secretaria")]
[Route("Celosan")]
public class CelosanAdminController : Controller
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStudentIdentityDocumentService _identityDocumentService;
    private readonly ICelosanBulkImportService _bulkImportService;
    private readonly ICelosanReportService _reportService;

    public CelosanAdminController(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        IStudentIdentityDocumentService identityDocumentService,
        ICelosanBulkImportService bulkImportService,
        ICelosanReportService reportService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityDocumentService = identityDocumentService;
        _bulkImportService = bulkImportService;
        _reportService = reportService;
    }

    [HttpGet("Documents")]
    public async Task<IActionResult> Documents()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var schoolId = currentUser?.SchoolId;
        ViewBag.Students = await _context.Users.AsNoTracking()
            .Where(u => (u.Role == "student" || u.Role == "estudiante" || u.Role == "alumno") && (!schoolId.HasValue || u.SchoolId == schoolId))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.Name)
            .ToListAsync();

        var documents = await _context.StudentIdentityDocuments
            .Include(d => d.Student)
            .AsNoTracking()
            .Where(d => !schoolId.HasValue || d.SchoolId == schoolId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(200)
            .ToListAsync();

        return View(documents);
    }

    [HttpPost("Documents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(Guid studentId, string? documentNumber, DateTime? expirationDate, IFormFile file)
    {
        try
        {
            await _identityDocumentService.SaveAsync(studentId, documentNumber, expirationDate, file);
            TempData["SuccessMessage"] = "Documento de identidad actualizado.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Documents));
    }

    [HttpGet("BulkCredits")]
    public async Task<IActionResult> BulkCredits()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var schoolId = currentUser?.SchoolId;
        var logs = await _context.CelosanBulkImportLogs.AsNoTracking()
            .Where(l => !schoolId.HasValue || l.SchoolId == schoolId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(100)
            .ToListAsync();
        return View(logs);
    }

    [HttpPost("BulkCredits")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBulkCredits(IFormFile file)
    {
        var result = await _bulkImportService.ImportApprovedCreditsAsync(file);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] =
            $"Procesadas: {result.ProcessedRows}. Exitosas: {result.SuccessRows}. Errores: {result.ErrorRows}.";
        if (result.Errors.Count > 0)
            TempData["ErrorDetails"] = string.Join(Environment.NewLine, result.Errors.Take(20));

        return RedirectToAction(nameof(BulkCredits));
    }

    [HttpGet("Reports")]
    public async Task<IActionResult> Reports()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var model = await _reportService.BuildDashboardAsync(currentUser?.SchoolId);
        return View(model);
    }
}
