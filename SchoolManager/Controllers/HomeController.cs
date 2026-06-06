using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using System.Diagnostics;

namespace SchoolManager.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUserService;
        private readonly SchoolDbContext _context;

        public HomeController(ILogger<HomeController> logger, IAuthService authService, ICurrentUserService currentUserService, SchoolDbContext context)
        {
            _logger = logger;
            _authService = authService;
            _currentUserService = currentUserService;
            _context = context;
        }

        public async Task<IActionResult> Index([FromQuery] Guid? schoolId)
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            ViewBag.UserName = currentUser?.Name;

            var userSchool = await _currentUserService.GetCurrentUserSchoolAsync();
            if (userSchool == null && schoolId.HasValue && schoolId.Value != Guid.Empty)
            {
                var selected = await _context.Schools.FindAsync(schoolId.Value);
                if (selected != null)
                {
                    ViewBag.SelectedSchool = selected;
                    ViewBag.Schools = new SelectList(await _context.Schools.OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync(), "Id", "Name", schoolId);
                }
                else
                    ViewBag.Schools = new SelectList(await _context.Schools.OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync(), "Id", "Name");
            }
            else if (userSchool == null)
            {
                ViewBag.Schools = new SelectList(await _context.Schools.OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync(), "Id", "Name");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
