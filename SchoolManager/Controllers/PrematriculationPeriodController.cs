using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SchoolManager.Dtos;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,superadmin")]
public class PrematriculationPeriodController : Controller
{
    private readonly IPrematriculationPeriodService _periodService;
    private readonly ICurrentUserService _currentUserService;
    private readonly SchoolDbContext _context;
    private readonly ILogger<PrematriculationPeriodController> _logger;

    public PrematriculationPeriodController(
        IPrematriculationPeriodService periodService,
        ICurrentUserService currentUserService,
        SchoolDbContext context,
        ILogger<PrematriculationPeriodController> logger)
    {
        _periodService = periodService;
        _currentUserService = currentUserService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var periods = await _periodService.GetAllAsync(currentUser.SchoolId.Value);
        return View(periods);
    }

    private async Task PopulatePeriodCatalogsAsync(Guid schoolId)
    {
        ViewBag.AcademicYears = await _context.AcademicYears.AsNoTracking()
            .Where(y => y.SchoolId == schoolId)
            .OrderByDescending(y => y.IsActive)
            .ThenByDescending(y => y.StartDate)
            .ToListAsync();
        ViewBag.Trimesters = await _context.Trimesters.AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .OrderBy(t => t.Order)
            .ThenBy(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        await PopulatePeriodCatalogsAsync(currentUser.SchoolId.Value);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(PrematriculationPeriodDto dto)
    {
        if (!ModelState.IsValid)
        {
            var invalidUser = await _currentUserService.GetCurrentUserAsync();
            if (invalidUser?.SchoolId != null)
                await PopulatePeriodCatalogsAsync(invalidUser.SchoolId.Value);
            return View(dto);
        }

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        try
        {
            var period = new PrematriculationPeriod
            {
                SchoolId = currentUser.SchoolId.Value,
                Name = dto.Name,
                AcademicYearId = dto.AcademicYearId,
                TrimesterId = dto.TrimesterId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsActive = dto.IsActive,
                MaxCapacityPerGroup = dto.MaxCapacityPerGroup,
                MaxSubjectsAllowed = dto.MaxSubjectsAllowed,
                AutoAssignByShift = dto.AutoAssignByShift
            };

            await _periodService.CreateAsync(period, currentUser.Id);
            
            TempData["SuccessMessage"] = "Período de prematrícula creado exitosamente";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear período de prematrícula");
            ModelState.AddModelError("", "Error al crear el período de prematrícula: " + ex.Message);
            await PopulatePeriodCatalogsAsync(currentUser.SchoolId.Value);
            return View(dto);
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var period = await _periodService.GetByIdAsync(id);
        if (period == null)
            return NotFound();

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null || period.SchoolId != currentUser.SchoolId.Value)
            return Unauthorized();

        var dto = new PrematriculationPeriodDto
        {
            Id = period.Id,
            SchoolId = period.SchoolId,
            Name = period.Name,
            AcademicYearId = period.AcademicYearId,
            TrimesterId = period.TrimesterId,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            IsActive = period.IsActive,
            MaxCapacityPerGroup = period.MaxCapacityPerGroup,
            MaxSubjectsAllowed = period.MaxSubjectsAllowed,
            AutoAssignByShift = period.AutoAssignByShift
        };

        await PopulatePeriodCatalogsAsync(currentUser.SchoolId.Value);
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(PrematriculationPeriodDto dto)
    {
        if (!ModelState.IsValid)
        {
            var invalidUser = await _currentUserService.GetCurrentUserAsync();
            if (invalidUser?.SchoolId != null)
                await PopulatePeriodCatalogsAsync(invalidUser.SchoolId.Value);
            return View(dto);
        }

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        try
        {
            var period = await _periodService.GetByIdAsync(dto.Id);
            if (period == null)
                return NotFound();

            if (currentUser.SchoolId == null || period.SchoolId != currentUser.SchoolId.Value)
                return Unauthorized();

            period.StartDate = dto.StartDate;
            period.Name = dto.Name;
            period.AcademicYearId = dto.AcademicYearId;
            period.TrimesterId = dto.TrimesterId;
            period.EndDate = dto.EndDate;
            period.IsActive = dto.IsActive;
            period.MaxCapacityPerGroup = dto.MaxCapacityPerGroup;
            period.MaxSubjectsAllowed = dto.MaxSubjectsAllowed;
            period.AutoAssignByShift = dto.AutoAssignByShift;

            await _periodService.UpdateAsync(period, currentUser.Id);
            
            TempData["SuccessMessage"] = "Período de prematrícula actualizado exitosamente";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar período de prematrícula");
            ModelState.AddModelError("", "Error al actualizar el período: " + ex.Message);
            if (currentUser.SchoolId != null)
                await PopulatePeriodCatalogsAsync(currentUser.SchoolId.Value);
            return View(dto);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var period = await _periodService.GetByIdAsync(id);
        if (period == null)
        {
            TempData["ErrorMessage"] = "Período no encontrado.";
            return RedirectToAction(nameof(Index));
        }

        if (currentUser?.SchoolId == null || period.SchoolId != currentUser.SchoolId.Value)
            return Unauthorized();

        var deleted = await _periodService.DeleteAsync(id);
        
        if (deleted)
            TempData["SuccessMessage"] = "Período eliminado exitosamente";
        else
            TempData["ErrorMessage"] = "No se puede eliminar el período porque tiene prematrículas asociadas";

        return RedirectToAction(nameof(Index));
    }
}

