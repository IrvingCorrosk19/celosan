using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "superadmin")]
[Route("SuperAdmin/Equivalencies")]
public class EquivalenciesController : Controller
{
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEquivalencyService _equivalencyService;

    public EquivalenciesController(
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        IEquivalencyService equivalencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _equivalencyService = equivalencyService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await PopulateCatalogsAsync();
        var pending = await _equivalencyService.GetPendingAsync();
        return View(pending);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid studentId, string externalInstitutionName, string? referenceDocument, string? notes)
    {
        var student = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId);
        if (student?.SchoolId == null)
        {
            TempData["ErrorMessage"] = "Estudiante no encontrado o sin escuela asociada.";
            return RedirectToAction(nameof(Index));
        }

        await _equivalencyService.CreateAsync(new StudentSubjectEquivalency
        {
            SchoolId = student.SchoolId.Value,
            StudentId = studentId,
            SourceInstitutionName = externalInstitutionName.Trim(),
            CertificateNumber = string.IsNullOrWhiteSpace(referenceDocument) ? null : referenceDocument.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = "Pending"
        });

        TempData["SuccessMessage"] = "Solicitud de convalidacion registrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AddItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(
        Guid equivalencyId,
        Guid curriculumSubjectId,
        string externalSubjectName,
        decimal? externalScore,
        decimal? normalizedScore)
    {
        await _equivalencyService.AddItemAsync(new StudentSubjectEquivalencyItem
        {
            EquivalencyId = equivalencyId,
            CurriculumSubjectId = curriculumSubjectId,
            ExternalSubjectName = externalSubjectName.Trim(),
            ExternalScore = externalScore?.ToString(),
            NormalizedScore = normalizedScore,
            Status = "Pending"
        });

        TempData["SuccessMessage"] = "Materia externa agregada para revision.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ApproveItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveItem(Guid itemId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "No se pudo resolver el usuario actual.";
            return RedirectToAction(nameof(Index));
        }

        var credit = await _equivalencyService.ApproveItemAsync(itemId, userId.Value);
        TempData[credit == null ? "ErrorMessage" : "SuccessMessage"] = credit == null
            ? "No se pudo aprobar la convalidacion."
            : "Convalidacion aprobada y credito academico creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("RejectItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectItem(Guid itemId)
    {
        var userId = await _currentUserService.GetCurrentUserIdAsync();
        if (userId.HasValue)
            await _equivalencyService.RejectItemAsync(itemId, userId.Value);

        TempData["SuccessMessage"] = "Convalidacion rechazada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCatalogsAsync()
    {
        ViewBag.Students = await _context.Users.AsNoTracking()
            .Where(u => u.Role == "student" || u.Role == "estudiante" || u.Role == "alumno")
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.Name)
            .ToListAsync();
        ViewBag.CurriculumSubjects = await _context.CurriculumSubjects
            .Include(cs => cs.Subject)
            .Include(cs => cs.GradeLevel)
            .AsNoTracking()
            .OrderBy(cs => cs.Subject.Name)
            .ToListAsync();
    }
}
