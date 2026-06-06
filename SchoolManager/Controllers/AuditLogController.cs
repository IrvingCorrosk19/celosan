using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;

[Authorize(Roles = "admin")]
public class AuditLogController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        var logs = await _auditLogService.GetAllAsync();
        return View(logs);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var log = await _auditLogService.GetByIdAsync(id);
        if (log == null) return NotFound();
        return View(log);
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(AuditLog log)
    {
        if (ModelState.IsValid)
        {
            await _auditLogService.LogActionAsync(log);
            return RedirectToAction(nameof(Index));
        }
        return View(log);
    }

    public async Task<IActionResult> LogsByUser(Guid userId)
    {
        var logs = await _auditLogService.GetByUserAsync(userId);
        return View("Index", logs);
    }
}
