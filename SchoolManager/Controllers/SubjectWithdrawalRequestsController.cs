using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "teacher,profesor,admin,superadmin")]
public class SubjectWithdrawalRequestsController : Controller
{
    private readonly SchoolDbContext _context;
    private readonly ISubjectWithdrawalRequestService _withdrawalRequestService;

    public SubjectWithdrawalRequestsController(SchoolDbContext context, ISubjectWithdrawalRequestService withdrawalRequestService)
    {
        _context = context;
        _withdrawalRequestService = withdrawalRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> FromAttendance(Guid attendanceId)
    {
        var attendance = await _context.Attendances
            .Include(a => a.Student)
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Id == attendanceId);
        if (attendance?.StudentId == null)
            return NotFound();

        var enrollments = await _context.StudentSubjectAssignments
            .Include(ssa => ssa.SubjectAssignment).ThenInclude(sa => sa.Subject)
            .Include(ssa => ssa.SubjectAssignment).ThenInclude(sa => sa.Group)
            .Where(ssa => ssa.StudentId == attendance.StudentId.Value &&
                          ssa.IsActive &&
                          (!attendance.GroupId.HasValue || ssa.SubjectAssignment.GroupId == attendance.GroupId.Value))
            .OrderBy(ssa => ssa.SubjectAssignment.Subject.Name)
            .ToListAsync();

        ViewBag.Attendance = attendance;
        return View(enrollments);
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
