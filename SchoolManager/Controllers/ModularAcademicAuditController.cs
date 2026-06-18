using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "superadmin")]
[Route("SuperAdmin/ModularAcademicAudit")]
public class ModularAcademicAuditController : Controller
{
    private readonly SchoolDbContext _context;

    public ModularAcademicAuditController(SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var studentsWithoutCurriculum = await _context.StudentAssignments
            .Where(sa => sa.IsActive)
            .Select(sa => sa.StudentId)
            .Distinct()
            .CountAsync(studentId => !_context.StudentAcademicPeriodEnrollments.Any(e =>
                e.StudentId == studentId && e.Status == "Active"));

        var activeSubjectsWithoutTrimester = await _context.StudentSubjectAssignments
            .CountAsync(ssa => ssa.IsActive && ssa.TrimesterId == null);

        var activeSubjectsWithoutCurriculumSubject = await _context.StudentSubjectAssignments
            .CountAsync(ssa => ssa.IsActive && ssa.CurriculumSubjectId == null);

        var creditsCreated = await _context.StudentAcademicCredits.CountAsync(c => c.Status == "Valid");

        var missingPrerequisiteRows = await _context.StudentSubjectAssignments
            .Where(ssa => ssa.IsActive && ssa.CurriculumSubjectId.HasValue)
            .Select(ssa => new
            {
                ssa.StudentId,
                CurriculumSubjectId = ssa.CurriculumSubjectId!.Value
            })
            .CountAsync(x => _context.CurriculumSubjectPrerequisites.Any(p =>
                p.CurriculumSubjectId == x.CurriculumSubjectId &&
                p.IsActive &&
                p.RequirementType == "Required" &&
                !_context.StudentAcademicCredits.Any(c =>
                    c.StudentId == x.StudentId &&
                    c.CurriculumSubjectId == p.PrerequisiteCurriculumSubjectId &&
                    c.Status == "Valid")));

        var blockedStudents = await _context.StudentSubjectAssignments
            .Where(ssa => ssa.ValidationStatus == "Blocked" || ssa.ValidationStatus == "PendingEquivalence")
            .Select(ssa => ssa.StudentId)
            .Distinct()
            .CountAsync();

        var pendingEquivalencies = await _context.StudentSubjectEquivalencyItems
            .CountAsync(i => i.Status == "Pending");

        var result = new ModularAcademicAuditResult(
            studentsWithoutCurriculum,
            activeSubjectsWithoutTrimester,
            activeSubjectsWithoutCurriculumSubject,
            creditsCreated,
            missingPrerequisiteRows,
            blockedStudents,
            pendingEquivalencies);

        return View(result);
    }
}
