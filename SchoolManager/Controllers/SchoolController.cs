using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;

[Authorize(Roles = "admin")]
public class SchoolController : Controller
{
    private readonly ISchoolService _schoolService;

    public SchoolController(ISchoolService schoolService)
    {
        _schoolService = schoolService;
    }

    public async Task<IActionResult> Index()
    {
        var schools = await _schoolService.GetAllAsync();
        return View(schools);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var school = await _schoolService.GetByIdAsync(id);
        if (school == null) return NotFound();
        return View(school);
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(School school)
    {
        if (ModelState.IsValid)
        {
            await _schoolService.CreateAsync(school);
            return RedirectToAction(nameof(Index));
        }
        return View(school);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var school = await _schoolService.GetByIdAsync(id);
        if (school == null) return NotFound();
        return View(school);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(School school)
    {
        if (ModelState.IsValid)
        {
            await _schoolService.UpdateAsync(school);
            return RedirectToAction(nameof(Index));
        }
        return View(school);
    }

    public async Task<IActionResult> Delete(Guid id)
    {
        var school = await _schoolService.GetByIdAsync(id);
        if (school == null) return NotFound();
        return View(school);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        await _schoolService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
