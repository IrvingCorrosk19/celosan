using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "Admin,Director,admin,director")]
[AutoValidateAntiforgeryToken]
public class ScheduleConfigurationController : Controller
{
    private readonly IScheduleConfigurationService _configService;
    private readonly ICurrentUserService _currentUserService;

    public ScheduleConfigurationController(
        IScheduleConfigurationService configService,
        ICurrentUserService currentUserService)
    {
        _configService = configService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (user?.SchoolId == null)
            return RedirectToAction("Index", "Home");

        var config = await _configService.GetBySchoolIdAsync(user.SchoolId.Value);
        if (config != null)
            config.RecessAfterAfternoonBlockNumber = config.RecessAfterMorningBlockNumber;

        var model = config ?? new SchoolScheduleConfiguration
        {
            SchoolId = user.SchoolId.Value,
            MorningStartTime = new TimeOnly(18, 0),
            MorningBlockDurationMinutes = 45,
            MorningBlockCount = 6,
            RecessDurationMinutes = 15,
            RecessAfterMorningBlockNumber = 3,
            RecessAfterAfternoonBlockNumber = 3,
            AfternoonStartTime = null,
            AfternoonBlockDurationMinutes = null,
            AfternoonBlockCount = null,
            NightStartTime = new TimeOnly(18, 0),
            NightBlockDurationMinutes = 45,
            NightBlockCount = 6
        };

        if (!model.NightStartTime.HasValue)
            model.NightStartTime = model.MorningStartTime == default ? new TimeOnly(18, 0) : model.MorningStartTime;
        model.NightBlockDurationMinutes ??= model.MorningBlockDurationMinutes > 0 ? model.MorningBlockDurationMinutes : 45;
        model.NightBlockCount ??= model.MorningBlockCount > 0 ? model.MorningBlockCount : 6;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConfiguration(SchoolScheduleConfiguration model, bool forceRegenerate = false)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (user?.SchoolId == null)
            return RedirectToAction("Index", "Home");

        model.SchoolId = user.SchoolId.Value;

        // La aplicación opera solo jornada nocturna; se ignoran mañana/tarde.
        var nightStr = Request.Form["NightStartTime"].ToString().Trim();
        if (!string.IsNullOrEmpty(nightStr) && TimeOnly.TryParse(nightStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var nightTime))
            model.NightStartTime = nightTime;

        var recessStr = Request.Form["RecessDurationMinutes"].ToString().Trim();
        if (!string.IsNullOrEmpty(recessStr) && int.TryParse(recessStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var recessMin))
            model.RecessDurationMinutes = Math.Clamp(recessMin, 1, 180);

        var afterBlockStr = Request.Form["RecessAfterMorningBlockNumber"].ToString().Trim();
        if (!string.IsNullOrEmpty(afterBlockStr) && int.TryParse(afterBlockStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var afterBlock))
            model.RecessAfterMorningBlockNumber = Math.Clamp(afterBlock, 1, 40);

        model.NightBlockDurationMinutes = Math.Max(1, model.NightBlockDurationMinutes ?? 45);
        model.NightBlockCount = Math.Max(1, model.NightBlockCount ?? 6);
        model.MorningStartTime = model.NightStartTime ?? new TimeOnly(18, 0);
        model.MorningBlockDurationMinutes = model.NightBlockDurationMinutes.Value;
        model.MorningBlockCount = model.NightBlockCount.Value;
        model.AfternoonStartTime = null;
        model.AfternoonBlockDurationMinutes = null;
        model.AfternoonBlockCount = null;
        model.RecessAfterAfternoonBlockNumber = model.RecessAfterMorningBlockNumber;

        var (success, message) = await _configService.SaveAndGenerateBlocksAsync(model, user.SchoolId.Value, forceRegenerate);
        if (success)
        {
            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = message;
        return View("Index", model);
    }
}
