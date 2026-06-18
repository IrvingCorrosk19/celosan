using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "teacher,profesor,admin,superadmin")]
public class SubjectWithdrawalRequestsController : Controller
{
    private readonly ISubjectWithdrawalRequestService _withdrawalRequestService;

    public SubjectWithdrawalRequestsController(ISubjectWithdrawalRequestService withdrawalRequestService)
    {
        _withdrawalRequestService = withdrawalRequestService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid studentSubjectAssignmentId, string reason, string? observation, string? returnUrl = null)
    {
        var result = await _withdrawalRequestService.RequestAsync(studentSubjectAssignmentId, reason, observation);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
