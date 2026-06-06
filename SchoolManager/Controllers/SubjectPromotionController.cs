using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "admin,director,secretaria")]
[Route("SubjectPromotion")]
public class SubjectPromotionController : Controller
{
    private readonly ISubjectPromotionService _promotionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly SchoolDbContext _context;

    public SubjectPromotionController(
        ISubjectPromotionService promotionService,
        ICurrentUserService currentUserService,
        SchoolDbContext context)
    {
        _promotionService = promotionService;
        _currentUserService = currentUserService;
        _context = context;
    }

    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("Records/{studentId:guid}")]
    public async Task<IActionResult> Records(Guid studentId)
    {
        var records = await _promotionService.GetRecordsByStudentAsync(studentId);
        return Json(new { success = true, data = records });
    }

    [HttpPost("Promote")]
    public async Task<IActionResult> Promote(
        Guid studentId,
        Guid studentSubjectAssignmentId,
        string trimester,
        decimal? finalScore,
        string? outcome)
    {
        var result = await _promotionService.PromoteSubjectAsync(
            studentId, studentSubjectAssignmentId, trimester, finalScore, outcome ?? "");
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost("CloseYear")]
    public async Task<IActionResult> CloseYear(Guid studentId, string trimester)
    {
        var result = await _promotionService.CloseYearForStudentAsync(studentId, trimester);
        return Json(new { success = result.Success, message = result.Message, processed = result.Processed });
    }

    [HttpGet("ActiveEnrollments/{studentId:guid}")]
    public async Task<IActionResult> ActiveEnrollments(Guid studentId)
    {
        var rows = await _context.StudentSubjectAssignments.AsNoTracking()
            .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
            .Join(_context.SubjectAssignments.AsNoTracking(),
                ssa => ssa.SubjectAssignmentId,
                sa => sa.Id,
                (ssa, sa) => new { ssa, sa })
            .Join(_context.Subjects.AsNoTracking(),
                x => x.sa.SubjectId,
                s => s.Id,
                (x, s) => new { x.ssa, x.sa, subject = s })
            .Join(_context.GradeLevels.AsNoTracking(),
                x => x.sa.GradeLevelId,
                g => g.Id,
                (x, g) => new
                {
                    enrollmentId = x.ssa.Id,
                    subjectName = x.subject.Name,
                    gradeName = g.Name,
                    enrollmentType = x.ssa.EnrollmentType,
                    status = x.ssa.Status
                })
            .OrderBy(x => x.subjectName)
            .ToListAsync();

        return Json(new { success = true, data = rows });
    }
}
