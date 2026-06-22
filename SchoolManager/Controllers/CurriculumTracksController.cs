using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,superadmin,secretaria")]
[Route("SuperAdmin/CurriculumTracks")]
public class CurriculumTracksController : Controller
{
    private readonly SchoolDbContext _context;
    private readonly ICurriculumService _curriculumService;

    public CurriculumTracksController(SchoolDbContext context, ICurriculumService curriculumService)
    {
        _context = context;
        _curriculumService = curriculumService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await PopulateCatalogsAsync();
        var tracks = await _curriculumService.GetTracksAsync();
        return View(tracks);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, Guid? schoolId, Guid? academicYearId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "El nombre de la malla es requerido.";
            return RedirectToAction(nameof(Index));
        }

        await _curriculumService.CreateTrackAsync(new CurriculumTrack
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SchoolId = schoolId,
            AcademicYearId = academicYearId,
            IsActive = true
        });

        TempData["SuccessMessage"] = "Malla curricular creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AddSubject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubject(
        Guid curriculumTrackId,
        Guid subjectId,
        Guid gradeLevelId,
        int moduleOrder,
        decimal credits,
        decimal minimumPassingScore)
    {
        await _curriculumService.AddSubjectAsync(new CurriculumSubject
        {
            CurriculumTrackId = curriculumTrackId,
            SubjectId = subjectId,
            GradeLevelId = gradeLevelId,
            ModuleOrder = moduleOrder,
            Credits = credits,
            MinimumPassingScore = minimumPassingScore,
            IsActive = true
        });

        TempData["SuccessMessage"] = "Materia curricular agregada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AddPrerequisite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPrerequisite(Guid curriculumSubjectId, Guid prerequisiteCurriculumSubjectId)
    {
        try
        {
            await _curriculumService.AddPrerequisiteAsync(new CurriculumSubjectPrerequisite
            {
                CurriculumSubjectId = curriculumSubjectId,
                PrerequisiteCurriculumSubjectId = prerequisiteCurriculumSubjectId,
                RequirementType = "Required",
                IsActive = true
            });
            TempData["SuccessMessage"] = "Prerrequisito creado.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCatalogsAsync()
    {
        ViewBag.Schools = await _context.Schools.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        ViewBag.AcademicYears = await _context.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
        ViewBag.Subjects = await _context.Subjects.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        ViewBag.GradeLevels = await _context.GradeLevels.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
        ViewBag.CurriculumSubjects = await _context.CurriculumSubjects
            .Include(cs => cs.Subject)
            .Include(cs => cs.GradeLevel)
            .AsNoTracking()
            .OrderBy(cs => cs.Subject.Name)
            .ToListAsync();
    }
}
