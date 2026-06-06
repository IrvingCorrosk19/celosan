using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

/// <summary>Portal académico de solo lectura para acudientes (calificaciones y pendientes).</summary>
[Authorize(Roles = "acudiente,parent")]
[Route("ParentAcademic")]
public class ParentAcademicController : Controller
{
    private readonly IStudentReportService _reportService;
    private readonly ICurrentUserService _currentUserService;
    private readonly SchoolDbContext _context;

    public ParentAcademicController(
        IStudentReportService reportService,
        ICurrentUserService currentUserService,
        SchoolDbContext context)
    {
        _reportService = reportService;
        _currentUserService = currentUserService;
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var parent = await _currentUserService.GetCurrentUserAsync();
        if (parent == null)
            return Unauthorized();

        var children = await GetLinkedStudentUsersAsync(parent.Id, parent.SchoolId);
        ViewBag.Children = children;
        return View();
    }

    [HttpGet("Report/{studentId:guid}")]
    public async Task<IActionResult> Report(Guid studentId, string? trimester = null)
    {
        var parent = await _currentUserService.GetCurrentUserAsync();
        if (parent == null)
            return Unauthorized();

        if (!await IsParentOfStudentAsync(parent.Id, studentId))
            return Forbid();

        var report = !string.IsNullOrWhiteSpace(trimester)
            ? await _reportService.GetReportByStudentIdAndTrimesterAsync(studentId, trimester)
            : await _reportService.GetReportByStudentIdAsync(studentId);

        if (report == null)
            return NotFound();

        return View(report);
    }

    private async Task<List<(Guid Id, string Name)>> GetLinkedStudentUsersAsync(Guid parentId, Guid? schoolId)
    {
        var fromPrematriculation = await _context.Prematriculations.AsNoTracking()
            .Where(p => p.ParentId == parentId)
            .Join(_context.Users.AsNoTracking(),
                p => p.StudentId,
                u => u.Id,
                (p, u) => new { u.Id, u.Name, u.LastName, u.SchoolId, u.Role })
            .Where(x => (x.Role.ToLower() == "student" || x.Role.ToLower() == "estudiante") &&
                        (!schoolId.HasValue || x.SchoolId == schoolId))
            .Select(x => new { x.Id, FullName = (x.Name ?? "") + " " + (x.LastName ?? "") })
            .ToListAsync();

        return fromPrematriculation
            .GroupBy(x => x.Id)
            .Select(g => (g.Key, g.First().FullName.Trim()))
            .OrderBy(x => x.Item2)
            .ToList();
    }

    private async Task<bool> IsParentOfStudentAsync(Guid parentId, Guid studentId)
    {
        var children = await GetLinkedStudentUsersAsync(parentId, null);
        return children.Any(c => c.Id == studentId);
    }
}
