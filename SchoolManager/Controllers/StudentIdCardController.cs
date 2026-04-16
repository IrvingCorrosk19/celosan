using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;
using SchoolManager.ViewModels;
using System.Security.Claims;

namespace SchoolManager.Controllers;

[Authorize(Roles = "SuperAdmin,superadmin")]
[Route("StudentIdCard")]
public class StudentIdCardController : Controller
{
    private const int BulkPrintMaxStudents = 30;

    private readonly IStudentIdCardService _service;
    private readonly IStudentIdCardPdfService _pdfService;
    private readonly IStudentIdCardHtmlCaptureService _htmlCapture;
    private readonly SchoolDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQrSignatureService _qrSignatureService;
    private readonly ILogger<StudentIdCardController> _logger;

    public StudentIdCardController(
        IStudentIdCardService service,
        IStudentIdCardPdfService pdfService,
        IStudentIdCardHtmlCaptureService htmlCapture,
        SchoolDbContext context,
        ICurrentUserService currentUserService,
        IQrSignatureService qrSignatureService,
        ILogger<StudentIdCardController> logger)
    {
        _service = service;
        _pdfService = pdfService;
        _htmlCapture = htmlCapture;
        _context = context;
        _currentUserService = currentUserService;
        _qrSignatureService = qrSignatureService;
        _logger = logger;
    }

    [HttpGet("ui")]
    public IActionResult Index() => View();

    /// <summary>
    /// BUG-2 fix: el GET solo lee y muestra el carnet actual — NO genera ni modifica estado.
    /// La generación ocurre exclusivamente vía POST a /api/generate/{studentId}.
    /// </summary>
    /// <summary>
    /// Vista previa del carnet: escuela y configuración siempre desde el estudiante (SchoolId del alumno),
    /// no desde la escuela del usuario autenticado (corrige SuperAdmin sin escuela).
    /// </summary>
    [HttpGet("ui/generate/{studentId}")]
    public async Task<IActionResult> GenerateView(Guid studentId)
    {
        var student = await StudentRoleFilter.WhereIsStudent(_context.Users.AsNoTracking())
            .Where(u => u.Id == studentId)
            .Select(u => new
            {
                u.SchoolId,
                u.DocumentId,
                u.EmergencyContactName,
                u.EmergencyContactPhone,
                u.Allergies,
                HasPaid = _context.StudentPaymentAccesses.Any(spa =>
                    spa.StudentId == u.Id && spa.CarnetStatus == "Pagado")
            })
            .FirstOrDefaultAsync();

        if (student == null)
        {
            return View("Generate", new StudentIdCardGenerateViewModel
            {
                StudentId = studentId,
                StudentNotFound = true,
                SchoolName = "—"
            });
        }

        if (!student.HasPaid)
        {
            _logger.LogWarning(
                "[StudentIdCard] GenerateView denegado: pago pendiente StudentId={StudentId}", studentId);
            return RedirectToAction(nameof(Index));
        }

        School? schoolEntity = null;
        SchoolIdCardSetting? cardSettings = null;
        var enabledTemplateFields = 0;
        string? academicYearName = null;

        if (student.SchoolId.HasValue)
        {
            var schoolId = student.SchoolId.Value;
            var bundle = await _context.Schools.AsNoTracking().IgnoreQueryFilters()
                .Where(s => s.Id == schoolId)
                .Select(s => new
                {
                    School = s,
                    CardSettings = _context.Set<SchoolIdCardSetting>().AsNoTracking().IgnoreQueryFilters()
                        .Where(x => x.SchoolId == s.Id)
                        .FirstOrDefault(),
                    EnabledFieldsCount = _context.Set<IdCardTemplateField>().AsNoTracking()
                        .Count(x => x.SchoolId == s.Id && x.IsEnabled),
                    AcademicYearName = _context.StudentAssignments
                        .Where(a => a.StudentId == studentId && a.IsActive)
                        .OrderByDescending(a => a.EnrollmentType != null && a.EnrollmentType.ToLower() == "nocturno")
                        .ThenByDescending(a => a.Shift != null && a.Shift.Name != null && a.Shift.Name.ToLower().Contains("noche"))
                        .ThenByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                        .Select(a => a.AcademicYear == null ? null : a.AcademicYear.Name)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (bundle != null)
            {
                schoolEntity = bundle.School;
                cardSettings = bundle.CardSettings;
                enabledTemplateFields = bundle.EnabledFieldsCount;
                academicYearName = bundle.AcademicYearName;
            }
        }

        var vm = StudentIdCardGenerateViewModel.ForStudent(studentId, schoolEntity, cardSettings);
        vm.UsesCustomPdfTemplate = enabledTemplateFields > 0;
        vm.EmergencyContactName = student.EmergencyContactName;
        vm.EmergencyContactPhone = student.EmergencyContactPhone;
        vm.Allergies = student.Allergies;
        vm.DocumentId = student.DocumentId;
        vm.PolicyNumber = string.IsNullOrWhiteSpace(schoolEntity?.PolicyNumber) ? null : schoolEntity!.PolicyNumber.Trim();
        vm.AcademicYear = academicYearName;

        var siteBase = $"{Request.Scheme}://{Request.Host.Value}";
        vm.Card = await _service.GetCurrentCardAsync(studentId, siteBase);
        return View("Generate", vm);
    }

    /// <summary>
    /// Página pública lectora (cualquier teléfono): datos de emergencia e información personal.
    /// Enlace firmado en el segundo QR del reverso del carnet.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("ScanApiPolicy")]
    [HttpGet("public/emergency-info")]
    public async Task<IActionResult> PublicEmergencyInfo([FromQuery] string? t)
    {
        if (!CarnetEmergencyInfoLink.TryResolveStudentId(t, _qrSignatureService, out var studentId))
            return View("PublicEmergencyInfoInvalid");

        var row = await StudentRoleFilter.WhereIsStudent(_context.Users.AsNoTracking())
            .Where(u => u.Id == studentId)
            .Select(u => new
            {
                u.Name,
                u.LastName,
                u.DocumentId,
                u.Email,
                u.DateOfBirth,
                u.CellphonePrimary,
                u.CellphoneSecondary,
                u.BloodType,
                u.EmergencyContactName,
                u.EmergencyContactPhone,
                u.EmergencyRelationship,
                u.Allergies,
                u.PhotoUrl,
                u.Shift,
                u.SchoolId,
                u.Status
            })
            .FirstOrDefaultAsync();

        if (row == null)
            return View("PublicEmergencyInfoInvalid");

        string? schoolName = null;
        if (row.SchoolId.HasValue)
        {
            schoolName = await _context.Schools.AsNoTracking().IgnoreQueryFilters()
                .Where(s => s.Id == row.SchoolId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
        }

        var assign = await _context.StudentAssignments.AsNoTracking()
            .Where(sa => sa.StudentId == studentId && sa.IsActive)
            .OrderByDescending(sa => sa.EnrollmentType != null && sa.EnrollmentType.ToLower() == "nocturno")
            .ThenByDescending(sa => sa.Shift != null && sa.Shift.Name != null && sa.Shift.Name.ToLower().Contains("noche"))
            .ThenByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
            .Select(sa => new
            {
                Grade = sa.Grade.Name,
                Group = sa.Group.Name,
                Shift = sa.Shift != null ? sa.Shift.Name : null
            })
            .FirstOrDefaultAsync();

        string? dob = null;
        if (row.DateOfBirth.HasValue)
            dob = row.DateOfBirth.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        var statusRaw = row.Status?.Trim();
        var isUserActive = string.Equals(statusRaw, "active", StringComparison.OrdinalIgnoreCase);

        var vm = new CarnetPublicEmergencyInfoVm
        {
            FullName = $"{row.Name} {row.LastName}".Trim(),
            DocumentId = row.DocumentId,
            Email = row.Email,
            DateOfBirthDisplay = dob,
            CellphonePrimary = row.CellphonePrimary,
            CellphoneSecondary = row.CellphoneSecondary,
            BloodType = row.BloodType,
            EmergencyContactName = row.EmergencyContactName,
            EmergencyContactPhone = row.EmergencyContactPhone,
            EmergencyRelationship = row.EmergencyRelationship,
            Allergies = row.Allergies,
            SchoolName = schoolName,
            Grade = assign?.Grade,
            Group = assign?.Group,
            Shift = assign?.Shift,
            UserShift = row.Shift,
            PhotoUrl = row.PhotoUrl,
            IsUserAccountActive = isUserActive,
            UserAccountStatusRaw = statusRaw
        };

        return View("PublicEmergencyInfo", vm);
    }

    [HttpGet("ui/scan")]
    public IActionResult Scan() => View();

    /// <summary>
    /// Descarga del PDF del carnet (frente/reverso según configuración). Errores en texto plano para que fetch en la vista pueda mostrarlos.
    /// </summary>
    [HttpGet("ui/print/{studentId}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Print(Guid studentId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Usuario no autenticado.");

        // PAY-GATE: bloquear generación de PDF sin pago confirmado
        var hasPaidPdf = await _context.StudentPaymentAccesses
            .AnyAsync(x => x.StudentId == studentId && x.CarnetStatus == "Pagado");
        if (!hasPaidPdf)
        {
            _logger.LogWarning(
                "[StudentIdCard] Print denegado: pago pendiente StudentId={StudentId}", studentId);
            return new ContentResult
            {
                Content = "Acceso denegado: el estudiante no ha pagado el carnet.",
                ContentType = "text/plain; charset=utf-8",
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        try
        {
            var url = $"{Request.Scheme}://{Request.Host}/StudentIdCard/ui/generate/{studentId}";
            try
            {
                var pdf = await _htmlCapture.GenerateFromUrl(url);
                await MarkCardAsPrintedAsync(studentId);
                return File(pdf, "application/pdf", $"carnet-{studentId:N}.pdf");
            }
            catch (Exception htmlEx)
            {
                // En Linux/Docker (p. ej. Render) a veces faltan .so de Chromium; intentar PDF nativo (Skia/QuestPDF).
                _logger.LogWarning(htmlEx,
                    "[StudentIdCard] PDF vía HTML/Chromium falló; usando generación nativa. StudentId={StudentId}",
                    studentId);
                var pdf = await _pdfService.GenerateCardPdfAsync(studentId, userId);
                await MarkCardAsPrintedAsync(studentId);
                return File(pdf, "application/pdf", $"carnet-{studentId:N}.pdf");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[StudentIdCard] Print/PDF error StudentId={StudentId} UserId={UserId}: {Message}",
                studentId, userId, ex.Message);
            return new ContentResult
            {
                Content = "No se pudo generar el PDF: " + ex.Message,
                ContentType = "text/plain; charset=utf-8",
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    [HttpPost("api/generate/{studentId}")]
    public async Task<IActionResult> GenerateApi(Guid studentId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Usuario no autenticado");

        // PAY-GATE doble guard (controller + service) — defensa en profundidad
        var hasPaidGen = await _context.StudentPaymentAccesses
            .AnyAsync(x => x.StudentId == studentId && x.CarnetStatus == "Pagado");
        if (!hasPaidGen)
        {
            _logger.LogWarning(
                "[StudentIdCard] GenerateApi denegado: pago pendiente StudentId={StudentId}", studentId);
            return BadRequest(new { message = "El estudiante no ha pagado el carnet." });
        }

        try
        {
            var siteBase = $"{Request.Scheme}://{Request.Host.Value}";
            var result = await _service.GenerateAsync(studentId, userId, siteBase);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("api/print-bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintBulk([FromBody] BulkPrintRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Usuario no autenticado.");

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var baseQuery = BuildEligibleStudentQuery(currentUser);

        var gradeFilter = string.IsNullOrWhiteSpace(request.Grade) ? null : request.Grade.Trim();
        var groupFilter = string.IsNullOrWhiteSpace(request.Group) ? null : request.Group.Trim();
        var shiftFilter = string.IsNullOrWhiteSpace(request.Shift) ? null : request.Shift.Trim();

        if (!string.IsNullOrWhiteSpace(gradeFilter))
            baseQuery = baseQuery.Where(u => u.StudentAssignments.Any(sa => sa.IsActive && sa.Grade != null && sa.Grade.Name == gradeFilter));
        if (!string.IsNullOrWhiteSpace(groupFilter))
            baseQuery = baseQuery.Where(u => u.StudentAssignments.Any(sa => sa.IsActive && sa.Group != null && sa.Group.Name == groupFilter));
        if (!string.IsNullOrWhiteSpace(shiftFilter))
            baseQuery = baseQuery.Where(u => u.StudentAssignments.Any(sa => sa.IsActive && sa.Shift != null && sa.Shift.Name == shiftFilter));

        var selectedIds = request.StudentIds?.Distinct().ToList() ?? new List<Guid>();
        if (selectedIds.Count == 0)
            return BadRequest(new { message = "Seleccione al menos un estudiante para imprimir." });

        if (selectedIds.Count > BulkPrintMaxStudents)
        {
            return BadRequest(new
            {
                message = $"Solo se permiten {BulkPrintMaxStudents} impresiones por operación. Reduzca la selección."
            });
        }

        baseQuery = baseQuery.Where(u => selectedIds.Contains(u.Id));

        var students = await baseQuery
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.Name)
            .Select(u => new { u.Id, FullName = $"{u.Name} {u.LastName}" })
            .Take(BulkPrintMaxStudents + 1)
            .ToListAsync();

        if (students.Count == 0)
            return BadRequest(new { message = "No hay estudiantes con esos filtros para impresión masiva." });

        if (students.Count > BulkPrintMaxStudents)
        {
            return BadRequest(new
            {
                message = $"La impresión masiva está limitada a {BulkPrintMaxStudents} estudiantes por operación. Ajuste los filtros."
            });
        }

        // Igual que Print() por estudiante: HTML /ui/generate/{id} → captura; si falla, nativo solo para ese id (ver GenerateBulkFromUrls).
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var generateUrls = students
            .Select(s => $"{baseUrl}/StudentIdCard/ui/generate/{s.Id}")
            .ToList();

        var studentPdfs = await _htmlCapture.GenerateBulkFromUrls(generateUrls);

        if (studentPdfs.Count != students.Count)
        {
            _logger.LogError(
                "[StudentIdCard] Bulk: se esperaban {Expected} PDFs, se obtuvieron {Actual}.",
                students.Count, studentPdfs.Count);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno al armar el PDF masivo." });
        }

        var merged = new PdfDocument();
        var printedStudentIds = new List<Guid>();
        for (var idx = 0; idx < students.Count; idx++)
        {
            var student = students[idx];
            try
            {
                var bytes = studentPdfs[idx];
                using var ms = new MemoryStream(bytes);
                using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
                for (var i = 0; i < doc.PageCount; i++)
                    merged.AddPage(doc.Pages[i]);
                printedStudentIds.Add(student.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[StudentIdCard] Bulk PDF failed for StudentId={StudentId}", student.Id);
            }
        }

        if (printedStudentIds.Count > 0)
            await MarkCardsAsPrintedAsync(printedStudentIds);

        if (merged.PageCount == 0)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "No se pudo generar el PDF masivo." });

        await using var outStream = new MemoryStream();
        merged.Save(outStream, false);
        var fileName = $"carnets-masivo-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return File(outStream.ToArray(), "application/pdf", fileName);
    }

    [HttpPost("api/print-status/{studentId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrintStatus(Guid studentId, [FromBody] UpdatePrintStatusRequestDto request)
    {
        var card = await _context.StudentIdCards
            .FirstOrDefaultAsync(c => c.StudentId == studentId && c.Status == "active");

        if (card == null)
            return NotFound(new { message = "No se encontró carnet activo para este estudiante." });

        card.IsPrinted = request.IsPrinted;
        card.PrintedAt = request.IsPrinted ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            isPrinted = card.IsPrinted,
            printedAt = card.PrintedAt
        });
    }

    /// <summary>
    /// Endpoint de escaneo QR. AllowAnonymous para compatibilidad con la APK móvil.
    /// CRÍTICO-1 fix: el rol se extrae del JWT autenticado (Cookie o Bearer token de la APK),
    /// NUNCA del cuerpo del request. Así un atacante no puede falsificar su rol.
    /// SEG-2: rate limiting aplicado vía política "ScanApiPolicy" (configurada en Program.cs).
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("ScanApiPolicy")]
    [HttpPost("api/scan")]
    public async Task<IActionResult> ScanApi([FromBody] ScanRequestDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(new { message = "Token es requerido" });

        // CRÍTICO-1 fix: poblar AuthenticatedRole desde el JWT del usuario autenticado.
        // Funciona con Cookie auth (portal web) y Bearer token (ApiBearerTokenMiddleware para APK).
        // Si la petición es anónima, AuthenticatedRole queda null → canSeeSensitiveData = false.
        dto.AuthenticatedRole = User.Identity?.IsAuthenticated == true
            ? User.FindFirst(ClaimTypes.Role)?.Value
            : null;

        try
        {
            var result = await _service.ScanAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// SCALE-4 fix: proyección SQL eficiente — sin doble evaluación de FirstOrDefault.
    /// </summary>
    [HttpGet("api/list-json")]
    public async Task<IActionResult> ListJson(string? grade = null, string? group = null, string? shift = null, string? printed = null)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var schoolId = currentUser?.SchoolId;
        var isSuperAdmin = currentUser?.Role != null &&
                           string.Equals(currentUser.Role, "superadmin", StringComparison.OrdinalIgnoreCase);
        _logger.LogInformation("[StudentIdCard/ListJson] Usuario={UserId} SchoolId={SchoolId} IsSuperAdmin={IsSuperAdmin}",
            currentUser?.Id, schoolId, isSuperAdmin);

        var query = BuildEligibleStudentQuery(currentUser);

        var gradeFilter = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim();
        var groupFilter = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
        var shiftFilter = string.IsNullOrWhiteSpace(shift) ? null : shift.Trim();
        var printedFilter = string.IsNullOrWhiteSpace(printed) ? null : printed.Trim().ToLowerInvariant();

        // Filtros por grado/grupo/jornada sobre asignación activa del estudiante.
        if (!string.IsNullOrWhiteSpace(gradeFilter))
        {
            query = query.Where(u => u.StudentAssignments
                .Any(sa => sa.IsActive && sa.Grade != null && sa.Grade.Name == gradeFilter));
        }
        if (!string.IsNullOrWhiteSpace(groupFilter))
        {
            query = query.Where(u => u.StudentAssignments
                .Any(sa => sa.IsActive && sa.Group != null && sa.Group.Name == groupFilter));
        }
        if (!string.IsNullOrWhiteSpace(shiftFilter))
        {
            query = query.Where(u => u.StudentAssignments
                .Any(sa => sa.IsActive && sa.Shift != null && sa.Shift.Name == shiftFilter));
        }
        if (printedFilter is "printed")
        {
            query = query.Where(u => _context.StudentIdCards
                .Any(c => c.StudentId == u.Id && c.Status == "active" && c.IsPrinted));
        }
        else if (printedFilter is "not_printed")
        {
            query = query.Where(u => !_context.StudentIdCards
                .Any(c => c.StudentId == u.Id && c.Status == "active" && c.IsPrinted));
        }

        // Una sola subconsulta a student_id_cards por fila (IsPrinted + PrintedAt) en lugar de dos correlacionadas.
        var rawRows = await query
            .Select(u => new
            {
                id = u.Id,
                fullName = $"{u.Name} {u.LastName}",
                photoUrl = u.PhotoUrl,
                grade = u.StudentAssignments
                    .Where(sa => sa.IsActive)
                    .OrderByDescending(sa => shiftFilter != null && sa.Shift != null && sa.Shift.Name == shiftFilter)
                    .ThenByDescending(sa => gradeFilter != null && sa.Grade != null && sa.Grade.Name == gradeFilter)
                    .ThenByDescending(sa => groupFilter != null && sa.Group != null && sa.Group.Name == groupFilter)
                    .ThenByDescending(sa => sa.EnrollmentType != null && sa.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(sa => sa.Shift != null && sa.Shift.Name != null && sa.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
                    .Select(sa => sa.Grade.Name)
                    .FirstOrDefault() ?? "Sin asignar",
                group = u.StudentAssignments
                    .Where(sa => sa.IsActive)
                    .OrderByDescending(sa => shiftFilter != null && sa.Shift != null && sa.Shift.Name == shiftFilter)
                    .ThenByDescending(sa => gradeFilter != null && sa.Grade != null && sa.Grade.Name == gradeFilter)
                    .ThenByDescending(sa => groupFilter != null && sa.Group != null && sa.Group.Name == groupFilter)
                    .ThenByDescending(sa => sa.EnrollmentType != null && sa.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(sa => sa.Shift != null && sa.Shift.Name != null && sa.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
                    .Select(sa => sa.Group.Name)
                    .FirstOrDefault() ?? "Sin asignar",
                shift = u.StudentAssignments
                    .Where(sa => sa.IsActive)
                    .OrderByDescending(sa => shiftFilter != null && sa.Shift != null && sa.Shift.Name == shiftFilter)
                    .ThenByDescending(sa => gradeFilter != null && sa.Grade != null && sa.Grade.Name == gradeFilter)
                    .ThenByDescending(sa => groupFilter != null && sa.Group != null && sa.Group.Name == groupFilter)
                    .ThenByDescending(sa => sa.EnrollmentType != null && sa.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(sa => sa.Shift != null && sa.Shift.Name != null && sa.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
                    .Select(sa => sa.Shift != null ? sa.Shift.Name : null)
                    .FirstOrDefault() ?? "Sin jornada",
                cardPrint = _context.StudentIdCards
                    .Where(c => c.StudentId == u.Id && c.Status == "active")
                    .Select(c => new { c.IsPrinted, c.PrintedAt })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var students = rawRows
            .Select(x => new
            {
                x.id,
                x.fullName,
                x.photoUrl,
                x.grade,
                x.group,
                x.shift,
                isPrinted = x.cardPrint != null && x.cardPrint.IsPrinted,
                printedAt = x.cardPrint == null ? (DateTime?)null : x.cardPrint.PrintedAt
            })
            .ToList();

        _logger.LogInformation(
            "[StudentIdCard/ListJson] Retornando {Count} estudiantes para SchoolId={SchoolId}",
            students.Count, schoolId);

        return Json(new { data = students });
    }

    [HttpGet("api/list-filters")]
    public async Task<IActionResult> ListFilters()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var query = BuildEligibleStudentQuery(currentUser);

        var baseAssignments = query.SelectMany(u => u.StudentAssignments.Where(sa => sa.IsActive));

        var grades = await baseAssignments
            .Select(sa => sa.Grade.Name)
            .Where(n => n != null && n != "")
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        var groups = await baseAssignments
            .Select(sa => sa.Group.Name)
            .Where(n => n != null && n != "")
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        var shifts = await baseAssignments
            .Select(sa => sa.Shift != null ? sa.Shift.Name : null)
            .Where(n => n != null && n != "")
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        return Json(new { grades, groups, shifts });
    }

    [HttpGet("api/list-ids")]
    public async Task<IActionResult> ListIds(string? grade = null, string? group = null, string? shift = null, string? printed = null)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        var query = BuildEligibleStudentQuery(currentUser);

        var gradeFilter = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim();
        var groupFilter = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
        var shiftFilter = string.IsNullOrWhiteSpace(shift) ? null : shift.Trim();
        var printedFilter = string.IsNullOrWhiteSpace(printed) ? null : printed.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(gradeFilter))
            query = query.Where(u => u.StudentAssignments.Any(sa => sa.IsActive && sa.Grade != null && sa.Grade.Name == gradeFilter));
        if (!string.IsNullOrWhiteSpace(groupFilter))
            query = query.Where(u => u.StudentAssignments.Any(sa => sa.IsActive && sa.Group != null && sa.Group.Name == groupFilter));
        if (!string.IsNullOrWhiteSpace(shiftFilter))
            query = query.Where(u => u.StudentAssignments.Any(sa => sa.IsActive && sa.Shift != null && sa.Shift.Name == shiftFilter));
        if (printedFilter is "printed")
            query = query.Where(u => _context.StudentIdCards.Any(c => c.StudentId == u.Id && c.Status == "active" && c.IsPrinted));
        else if (printedFilter is "not_printed")
            query = query.Where(u => !_context.StudentIdCards.Any(c => c.StudentId == u.Id && c.Status == "active" && c.IsPrinted));

        var ids = await query.Select(u => u.Id).ToListAsync();
        return Json(new { ids });
    }

    private IQueryable<User> BuildEligibleStudentQuery(User? currentUser)
    {
        var schoolId = currentUser?.SchoolId;
        var isSuperAdmin = currentUser?.Role != null &&
            string.Equals(currentUser.Role, "superadmin", StringComparison.OrdinalIgnoreCase);

        var query = StudentRoleFilter.WhereIsStudent(_context.Users)
            .Where(u => _context.StudentPaymentAccesses
                .Any(spa => spa.StudentId == u.Id && spa.CarnetStatus == "Pagado"));

        if (schoolId.HasValue && !isSuperAdmin)
            query = query.Where(u => u.SchoolId == schoolId.Value);

        return query;
    }

    private Task MarkCardAsPrintedAsync(Guid studentId) =>
        MarkCardsAsPrintedAsync(new[] { studentId });

    private async Task MarkCardsAsPrintedAsync(IReadOnlyList<Guid> studentIds)
    {
        if (studentIds == null || studentIds.Count == 0)
            return;

        var distinct = studentIds.Distinct().ToList();
        var cards = await _context.StudentIdCards
            .Where(c => distinct.Contains(c.StudentId) && c.Status == "active")
            .ToListAsync();

        if (cards.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var c in cards)
        {
            c.IsPrinted = true;
            c.PrintedAt = now;
        }

        await _context.SaveChangesAsync();
    }
}

public class BulkPrintRequestDto
{
    public string? Grade { get; set; }
    public string? Group { get; set; }
    public string? Shift { get; set; }
    public List<Guid>? StudentIds { get; set; }
}

public class UpdatePrintStatusRequestDto
{
    public bool IsPrinted { get; set; }
}
