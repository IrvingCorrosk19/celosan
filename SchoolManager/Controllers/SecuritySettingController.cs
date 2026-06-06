using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

[Authorize(Roles = "admin")]
public class SecuritySettingController : Controller
{
    private readonly ISecuritySettingService _securitySettingService;
    private readonly ISchoolService _schoolService;

    public SecuritySettingController(ISecuritySettingService securitySettingService, ISchoolService schoolService)
    {
        _securitySettingService = securitySettingService;
        _schoolService = schoolService;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _securitySettingService.GetAllAsync();
        return View(settings);
    }

    public async Task<IActionResult> Details(Guid schoolId)
    {
        var setting = await _securitySettingService.GetBySchoolIdAsync(schoolId);
        if (setting == null) return NotFound();
        return View(setting);
    }

    public async Task<IActionResult> Create()
    {
        // Obtener lista de escuelas para el dropdown
        var schools = await _schoolService.GetAllAsync();
        ViewBag.Schools = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(schools, "Id", "Name");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(SecuritySetting setting)
    {
        if (ModelState.IsValid)
        {
            await _securitySettingService.CreateAsync(setting);
            TempData["SuccessMessage"] = "Configuración de seguridad creada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        var schools = await _schoolService.GetAllAsync();
        ViewBag.Schools = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(schools, "Id", "Name");
        return View(setting);
    }

    public async Task<IActionResult> Edit(Guid schoolId)
    {
        var setting = await _securitySettingService.GetBySchoolIdAsync(schoolId);
        if (setting == null) return NotFound();
        return View(setting);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(SecuritySetting setting)
    {
        if (ModelState.IsValid)
        {
            await _securitySettingService.UpdateAsync(setting);
            return RedirectToAction(nameof(Index));
        }
        return View(setting);
    }
}
