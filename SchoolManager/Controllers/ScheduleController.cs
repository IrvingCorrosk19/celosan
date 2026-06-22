using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize(Roles = "Teacher,Admin,Director,teacher,admin,director")]
[AutoValidateAntiforgeryToken]
public class ScheduleController : Controller
{
    private readonly IScheduleService _scheduleService;
    private readonly ICurrentUserService _currentUserService;
    private readonly SchoolDbContext _context;
    private readonly IAcademicYearService _academicYearService;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(
        IScheduleService scheduleService,
        ICurrentUserService currentUserService,
        SchoolDbContext context,
        IAcademicYearService academicYearService,
        ILogger<ScheduleController> logger)
    {
        _scheduleService = scheduleService;
        _currentUserService = currentUserService;
        _context = context;
        _academicYearService = academicYearService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Vista de horario por docente. Teacher solo ve el suyo; Admin/Director pueden elegir docente.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ByTeacher(Guid? teacherId, Guid? academicYearId)
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (user == null || user.SchoolId == null)
            return RedirectToAction("Index", "Home");

        var role = await _currentUserService.GetCurrentUserRoleAsync();
        var isTeacher = string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(role, "docente", StringComparison.OrdinalIgnoreCase);

        Guid effectiveTeacherId;
        if (isTeacher)
            effectiveTeacherId = user.Id;
        else
            effectiveTeacherId = teacherId ?? user.Id;

        var timeSlots = await _context.TimeSlots
            .Where(t => t.SchoolId == user.SchoolId && t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.StartTime)
            .Select(t => new { t.Id, t.Name, t.StartTime, t.EndTime, t.DisplayOrder, ShiftName = t.Shift != null ? t.Shift.Name : (string?)null })
            .ToListAsync();

        var academicYears = await _academicYearService.GetAllBySchoolAsync(user.SchoolId.Value);
        _logger.LogInformation("[Schedule/ByTeacher] SchoolId={SchoolId}, AcademicYearsCount={Count}. Si el desplegable no muestra años, verifique que existan registros en academic_years para esta escuela.", user.SchoolId, academicYears.Count);

        List<object> teachers = new List<object>();
        if (!isTeacher)
        {
            var teacherUsers = await _context.Users
                .Where(u => u.SchoolId == user.SchoolId && (u.Role == "teacher" || u.Role == "docente"))
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.Name)
                .Select(u => new { u.Id, u.Name, u.LastName })
                .ToListAsync();
            teachers = teacherUsers.Select(u => (object)new { u.Id, displayName = $"{u.Name} {u.LastName}".Trim() }).ToList();
        }

        // Jornada(s) en que imparte el docente: por los grupos de sus asignaciones (Group -> Shift)
        var teacherShiftNames = await _context.TeacherAssignments
            .Where(ta => ta.TeacherId == effectiveTeacherId)
            .Select(ta => ta.SubjectAssignment!.Group!.ShiftNavigation)
            .Where(s => s != null)
            .Select(s => s!.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
        ViewBag.TeacherShiftNames = teacherShiftNames;

        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        ViewBag.TimeSlotsJson = System.Text.Json.JsonSerializer.Serialize(timeSlots, jsonOptions);
        ViewBag.AcademicYearsJson = System.Text.Json.JsonSerializer.Serialize(academicYears.Select(a => new { a.Id, a.Name }), jsonOptions);
        ViewBag.TeachersJson = System.Text.Json.JsonSerializer.Serialize(teachers, jsonOptions);
        ViewBag.TeacherId = effectiveTeacherId;
        ViewBag.AcademicYearId = academicYearId ?? Guid.Empty;
        ViewBag.IsEditable = isTeacher ||
                             string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(role, "director", StringComparison.OrdinalIgnoreCase);
        ViewBag.IsTeacher = isTeacher;
        ViewBag.HasNoAcademicYears = academicYears.Count == 0;
        return View();
    }

    /// <summary>
    /// Devuelve los bloques horarios de la escuela del usuario actual (para construir la tabla).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListJsonTimeSlots()
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        if (user?.SchoolId == null)
            return Json(new { success = false, message = "Usuario sin escuela.", data = (object?)null });

        var slots = await _context.TimeSlots
            .Where(t => t.SchoolId == user.SchoolId && t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.StartTime)
            .Select(t => new { t.Id, t.Name, startTime = t.StartTime.ToString("HH:mm"), endTime = t.EndTime.ToString("HH:mm"), t.DisplayOrder })
            .ToListAsync();
        return Json(new { success = true, message = "", data = slots });
    }

    /// <summary>
    /// Lista entradas de horario por docente. Si el rol es Teacher, solo se permite el docente actual.
    /// Admin/Director solo pueden consultar docentes de su misma escuela.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListJsonByTeacher(Guid teacherId, Guid academicYearId)
    {
        try
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.SchoolId == null)
                return Json(new { success = false, message = "Usuario no autenticado o sin escuela.", data = (object?)null });

            var role = await _currentUserService.GetCurrentUserRoleAsync();
            var isTeacher = string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(role, "docente", StringComparison.OrdinalIgnoreCase);

            Guid effectiveTeacherId;
            if (isTeacher)
            {
                if (teacherId != currentUser.Id)
                    return Json(new { success = false, message = "Solo puede consultar su propio horario.", data = (object?)null });
                effectiveTeacherId = currentUser.Id;
            }
            else
            {
                // Multi-tenant: verificar que el docente pertenezca a la misma escuela
                var teacherBelongsToSchool = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == teacherId && u.SchoolId == currentUser.SchoolId);
                if (!teacherBelongsToSchool)
                    return Json(new { success = false, message = "El docente no pertenece a su escuela.", data = (object?)null });
                effectiveTeacherId = teacherId;
            }

            if (academicYearId == Guid.Empty)
                return Json(new { success = false, message = "Debe indicar el año académico.", data = (object?)null });

            var entries = await _scheduleService.GetByTeacherAsync(effectiveTeacherId, academicYearId);
            var data = entries.Select(MapEntryToJson).ToList();
            return Json(new { success = true, message = "", data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    /// <summary>
    /// Lista entradas de horario por grupo. Admin/Director pueden consultar grupos de su misma escuela.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListJsonByGroup(Guid groupId, Guid academicYearId)
    {
        try
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            if (currentUser == null || currentUser.SchoolId == null)
                return Json(new { success = false, message = "Usuario no autenticado o sin escuela.", data = (object?)null });

            if (groupId == Guid.Empty)
                return Json(new { success = false, message = "Debe indicar el grupo.", data = (object?)null });
            if (academicYearId == Guid.Empty)
                return Json(new { success = false, message = "Debe indicar el año académico.", data = (object?)null });

            // Multi-tenant: verificar que el grupo pertenezca a la misma escuela
            var groupBelongsToSchool = await _context.Groups.AsNoTracking()
                .AnyAsync(g => g.Id == groupId && g.SchoolId == currentUser.SchoolId);
            if (!groupBelongsToSchool)
                return Json(new { success = false, message = "El grupo no pertenece a su escuela.", data = (object?)null });

            var entries = await _scheduleService.GetByGroupAsync(groupId, academicYearId);
            var data = entries.Select(MapEntryToJson).ToList();
            return Json(new { success = true, message = "", data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveEntry([FromBody] SaveEntryRequest request)
    {
        try
        {
            var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
            if (currentUserId == null)
                return Json(new { success = false, message = "Usuario no autenticado.", data = (object?)null });

            if (request == null ||
                request.TeacherAssignmentId == Guid.Empty ||
                request.TimeSlotId == Guid.Empty ||
                request.AcademicYearId == Guid.Empty ||
                request.DayOfWeek < 1 || request.DayOfWeek > 7)
                return Json(new { success = false, message = "Datos inválidos (asignación, bloque, año y día 1-7).", data = (object?)null });

            var entry = await _scheduleService.CreateEntryAsync(
                request.TeacherAssignmentId,
                request.TimeSlotId,
                request.DayOfWeek,
                request.AcademicYearId,
                currentUserId.Value);

            var data = MapEntryToJson(entry);
            return Json(new { success = true, message = "Entrada guardada.", data });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEntry([FromBody] DeleteEntryRequest request)
    {
        try
        {
            var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
            if (currentUserId == null)
                return Json(new { success = false, message = "Usuario no autenticado.", data = (object?)null });

            if (request == null || request.Id == Guid.Empty)
                return Json(new { success = false, message = "ID de entrada inválido.", data = (object?)null });

            await _scheduleService.DeleteEntryAsync(request.Id, currentUserId.Value);
            return Json(new { success = true, message = "Entrada eliminada.", data = (object?)null });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message, data = (object?)null });
        }
    }

    private static object MapEntryToJson(ScheduleEntry e)
    {
        var ta = e.TeacherAssignment;
        var sa = ta?.SubjectAssignment;
        var subjectName = sa?.Subject?.Name;
        var groupName = sa?.Group?.Name;
        var teacherName = ta?.Teacher != null ? $"{ta.Teacher.Name} {ta.Teacher.LastName}".Trim() : null;
        var timeSlot = e.TimeSlot;
        return new
        {
            id = e.Id,
            teacherAssignmentId = e.TeacherAssignmentId,
            timeSlotId = e.TimeSlotId,
            dayOfWeek = e.DayOfWeek,
            academicYearId = e.AcademicYearId,
            subjectName,
            groupName,
            teacherName,
            timeSlotName = timeSlot?.Name,
            startTime = timeSlot != null ? timeSlot.StartTime.ToString("HH\\:mm") : null,
            endTime = timeSlot != null ? timeSlot.EndTime.ToString("HH\\:mm") : null,
            createdAt = e.CreatedAt
        };
    }
}

public class SaveEntryRequest
{
    public Guid TeacherAssignmentId { get; set; }
    public Guid TimeSlotId { get; set; }
    public byte DayOfWeek { get; set; }
    public Guid AcademicYearId { get; set; }
}

public class DeleteEntryRequest
{
    public Guid Id { get; set; }
}
