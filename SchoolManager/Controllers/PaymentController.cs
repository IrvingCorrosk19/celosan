using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Interfaces;

namespace SchoolManager.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly IPrematriculationService _prematriculationService;
    private readonly IPaymentConceptService _paymentConceptService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IMessagingService? _messagingService;
    private readonly SchoolDbContext _context;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        IPrematriculationService prematriculationService,
        IPaymentConceptService paymentConceptService,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        ICloudinaryService cloudinaryService,
        IMessagingService? messagingService,
        SchoolDbContext context,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _prematriculationService = prematriculationService;
        _paymentConceptService = paymentConceptService;
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _cloudinaryService = cloudinaryService;
        _messagingService = messagingService;
        _context = context;
        _logger = logger;
    }

    [Authorize(Roles = "admin,superadmin,contabilidad,contable")]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var payments = await _paymentService.GetBySchoolAsync(currentUser.SchoolId.Value);
        return View(payments);
    }

    // Vista para acudiente pagar desde portal
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> MyPayments()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var userRole = currentUser.Role?.ToLower() ?? "";
        List<PaymentDto> payments;

        if (userRole == "student" || userRole == "estudiante")
        {
            // Buscar pagos del estudiante
            payments = await _paymentService.GetByStudentAsync(currentUser.Id);
        }
        else
        {
            // Buscar pagos de los hijos del acudiente
            payments = await _paymentService.GetByParentAsync(currentUser.Id);
        }

        ViewBag.CurrentUserRole = userRole;
        return View(payments);
    }

    // Vista para acudiente crear pago desde portal
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> PayFromPortal(Guid? prematriculationId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var userRole = currentUser.Role?.ToLower() ?? "";

        // Obtener prematrículas del estudiante/acudiente
        List<Prematriculation> prematriculations = new List<Prematriculation>();
        if (userRole == "student" || userRole == "estudiante")
        {
            var studentPrematriculations = await _prematriculationService.GetByStudentAsync(currentUser.Id);
            var prematriculationIds = studentPrematriculations.Select(p => p.Id).ToList();
            if (prematriculationIds.Any())
            {
                prematriculations = await _context.Prematriculations
                    .Where(p => prematriculationIds.Contains(p.Id) && p.Status != "Matriculado")
                    .Include(p => p.Student)
                    .Include(p => p.Grade)
                    .ToListAsync();
            }
        }
        else
        {
            var parentPrematriculations = await _prematriculationService.GetByParentAsync(currentUser.Id);
            var prematriculationIds = parentPrematriculations.Select(p => p.Id).ToList();
            if (prematriculationIds.Any())
            {
                prematriculations = await _context.Prematriculations
                    .Where(p => prematriculationIds.Contains(p.Id) && p.Status != "Matriculado")
                    .Include(p => p.Student)
                    .Include(p => p.Grade)
                    .ToListAsync();
            }
        }

        ViewBag.Prematriculations = prematriculations;

        // Si hay una prematrícula específica, obtener sus datos
        Prematriculation? selectedPrematriculation = null;
        List<PaymentDto>? existingPayments = null;
        if (prematriculationId.HasValue)
        {
            selectedPrematriculation = await _prematriculationService.GetByIdAsync(prematriculationId.Value);
            if (selectedPrematriculation != null)
            {
                existingPayments = await _paymentService.GetByPrematriculationAsync(prematriculationId.Value);
            }
        }

        ViewBag.SelectedPrematriculation = selectedPrematriculation;
        ViewBag.ExistingPayments = existingPayments;

        // Obtener conceptos de pago activos
        var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
        ViewBag.PaymentConcepts = paymentConcepts;

        return View(new PaymentCreateDto 
        { 
            PrematriculationId = prematriculationId,
            PaymentDate = DateTime.UtcNow
        });
    }

    [HttpPost]
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> PayFromPortal(PaymentCreateDto dto, IFormFile? receiptImageFile)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var prematriculation = await _prematriculationService.GetByIdAsync(dto.PrematriculationId ?? Guid.Empty);
        if (prematriculation == null && dto.PrematriculationId.HasValue)
        {
            ModelState.AddModelError("", "Prematrícula no encontrada");
            return RedirectToAction(nameof(PayFromPortal));
        }

        // Validar que la prematrícula pertenezca al usuario
        if (prematriculation != null)
        {
            var userRole = currentUser.Role?.ToLower() ?? "";
            var isAuthorized = false;

            if (userRole == "student" || userRole == "estudiante")
            {
                isAuthorized = prematriculation.StudentId == currentUser.Id;
            }
            else
            {
                isAuthorized = prematriculation.ParentId == currentUser.Id;
            }

            if (!isAuthorized)
            {
                TempData["ErrorMessage"] = "No tiene permiso para realizar pagos de esta prematrícula";
                return RedirectToAction(nameof(MyPayments));
            }
        }

        // Comprobantes: solo Cloudinary (cualquier ambiente).
        if (receiptImageFile != null && receiptImageFile.Length > 0)
        {
            try
            {
                if (!_cloudinaryService.IsConfigured)
                {
                    ModelState.AddModelError("ReceiptImage",
                        "Almacenamiento de comprobantes no disponible: configure Cloudinary (CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, CLOUDINARY_API_SECRET).");
                }
                else
                {
                    var imageUrl = await _cloudinaryService.UploadImageAsync(receiptImageFile, "payments/receipts");
                    if (!string.IsNullOrEmpty(imageUrl))
                        dto.ReceiptImage = imageUrl;
                    else
                        ModelState.AddModelError("ReceiptImage", "No se pudo subir el comprobante a Cloudinary. Intente de nuevo.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen del comprobante");
                ModelState.AddModelError("ReceiptImage", "Error al subir la imagen: " + ex.Message);
            }
        }

        // Validar que métodos manuales requieren comprobante
        if (!string.IsNullOrEmpty(dto.PaymentMethod) && 
            (dto.PaymentMethod == "Transferencia" || dto.PaymentMethod == "Depósito" || dto.PaymentMethod == "Yappy") &&
            string.IsNullOrEmpty(dto.ReceiptImage))
        {
            ModelState.AddModelError("ReceiptImage", "Los métodos de pago manuales requieren adjuntar un comprobante");
        }

        // Generar número de recibo si es pago con tarjeta (automático)
        // ⚠️ MODO SIMULADO: En producción, el número de recibo vendría de la pasarela de pagos
        if (string.IsNullOrEmpty(dto.ReceiptNumber) && dto.PaymentMethod == "Tarjeta")
        {
            dto.ReceiptNumber = $"REC-SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(PayFromPortal), new { prematriculationId = dto.PrematriculationId });
        }

        try
        {
            var payment = await _paymentService.CreateAsync(dto, currentUser.Id);
            
            // Enviar notificación a contabilidad si el pago es pendiente
            if (payment.PaymentStatus == "Pendiente" && _messagingService != null)
            {
                try
                {
                    // Obtener usuarios de contabilidad de la escuela
                    var accountingUsers = await _context.Users
                        .Where(u => (u.Role.ToLower() == "contable" || u.Role.ToLower() == "contabilidad") 
                            && u.SchoolId == currentUser.SchoolId.Value)
                        .ToListAsync();

                    var studentName = prematriculation?.Student != null ? 
                        $"{prematriculation.Student.Name} {prematriculation.Student.LastName}" : "Estudiante";

                    foreach (var accountingUser in accountingUsers)
                    {
                        var messageModel = new SchoolManager.ViewModels.SendMessageViewModel
                        {
                            RecipientType = "Individual",
                            RecipientId = accountingUser.Id,
                            Subject = $"💰 Pago Pendiente de Verificación - {studentName}",
                            Content = $@"
<h3>Pago Pendiente de Verificación</h3>
<p>Se ha registrado un nuevo pago que requiere verificación:</p>
<ul>
    <li><strong>Estudiante:</strong> {studentName}</li>
    <li><strong>Monto:</strong> {dto.Amount:C}</li>
    <li><strong>Método de Pago:</strong> {dto.PaymentMethod ?? "N/A"}</li>
    <li><strong>Número de Recibo:</strong> {payment.ReceiptNumber}</li>
</ul>
<p>Por favor, revise el comprobante y confirme el pago cuando sea validado.</p>
"
                        };

                        await _messagingService.SendMessageAsync(messageModel, currentUser.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar notificación de pago pendiente");
                }
            }
            
            if (payment.PaymentStatus == "Confirmado")
            {
                TempData["SuccessMessage"] = $"Pago realizado exitosamente. Recibo: {payment.ReceiptNumber}. La matrícula se activará automáticamente.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Pago registrado exitosamente. Recibo: {payment.ReceiptNumber}. Está pendiente de verificación por contabilidad.";
            }
            
            return RedirectToAction(nameof(MyPayments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar pago desde portal");
            TempData["ErrorMessage"] = "Error al registrar el pago: " + ex.Message;
            return RedirectToAction(nameof(PayFromPortal), new { prematriculationId = dto.PrematriculationId });
        }
    }

    // Vista dedicada para pago inmediato con tarjeta tras la prematrícula
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> PayWithCard(Guid prematriculationId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        if (prematriculationId == Guid.Empty)
            return RedirectToAction(nameof(MyPayments));

        var prematriculation = await _prematriculationService.GetByIdAsync(prematriculationId);
        if (prematriculation == null)
        {
            TempData["ErrorMessage"] = "Prematrícula no encontrada.";
            return RedirectToAction(nameof(MyPayments));
        }

        var userRole = currentUser.Role?.ToLower() ?? "";
        var isAuthorized = (userRole == "student" || userRole == "estudiante")
            ? prematriculation.StudentId == currentUser.Id
            : prematriculation.ParentId == currentUser.Id;

        if (!isAuthorized)
            return Forbid();

        if (prematriculation.Status == "Matriculado")
        {
            TempData["InfoMessage"] = "Esta prematrícula ya está matriculada. No requiere pago adicional.";
            return RedirectToAction(nameof(MyPayments));
        }

        var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
        ViewBag.SelectedPrematriculation = prematriculation;
        ViewBag.PaymentConcepts = paymentConcepts;

        // Mostrar pagos previos por transparencia
        var existingPayments = await _paymentService.GetByPrematriculationAsync(prematriculationId);
        ViewBag.ExistingPayments = existingPayments;

        TempData["InfoMessage"] ??= "Prematrícula creada exitosamente. Complete el pago con tarjeta para confirmar la matrícula.";

        return View(new PaymentCreateDto
        {
            PrematriculationId = prematriculationId,
            PaymentDate = DateTime.UtcNow
        });
    }

    [HttpPost]
    [Authorize(Roles = "acudiente,parent,student,estudiante")]
    public async Task<IActionResult> PayWithCard(PaymentCreateDto dto)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        if (!dto.PrematriculationId.HasValue || dto.PrematriculationId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Debe especificar una prematrícula para procesar el pago.";
            return RedirectToAction(nameof(MyPayments));
        }

        var prematriculation = await _prematriculationService.GetByIdAsync(dto.PrematriculationId.Value);
        if (prematriculation == null)
        {
            TempData["ErrorMessage"] = "Prematrícula no encontrada.";
            return RedirectToAction(nameof(MyPayments));
        }

        var userRole = currentUser.Role?.ToLower() ?? "";
        var isAuthorized = (userRole == "student" || userRole == "estudiante")
            ? prematriculation.StudentId == currentUser.Id
            : prematriculation.ParentId == currentUser.Id;

        if (!isAuthorized)
            return Forbid();

        if (dto.Amount <= 0)
        {
            TempData["ErrorMessage"] = "El monto debe ser mayor que cero.";
            return RedirectToAction(nameof(PayWithCard), new { prematriculationId = dto.PrematriculationId });
        }

        // Forzar método Tarjeta y generar recibo simulado
        dto.PaymentMethod = "Tarjeta";
        if (string.IsNullOrEmpty(dto.ReceiptNumber))
        {
            dto.ReceiptNumber = $"REC-SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }

        try
        {
            var payment = await _paymentService.CreateAsync(dto, currentUser.Id);
            TempData["SuccessMessage"] = $"Pago con tarjeta confirmado. Recibo: {payment.ReceiptNumber}. La matrícula se activará automáticamente.";
            return RedirectToAction(nameof(MyPayments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar pago inmediato con tarjeta");
            TempData["ErrorMessage"] = "No se pudo procesar el pago con tarjeta: " + ex.Message;
            return RedirectToAction(nameof(PayWithCard), new { prematriculationId = dto.PrematriculationId });
        }
    }

    // Buscar prematrícula por código o estudiante
    [HttpGet]
    public async Task<IActionResult> Search()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        // Obtener conceptos de pago activos para el dropdown
        var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
        ViewBag.PaymentConcepts = paymentConcepts;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Search(string searchTerm)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        // Cargar conceptos de pago activos una sola vez
        var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
        ViewBag.PaymentConcepts = paymentConcepts;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            ModelState.AddModelError("", "Por favor ingrese un código de prematrícula o nombre de estudiante");
            return View();
        }

        Prematriculation? prematriculation = null;

        // Buscar por código
        if (searchTerm.StartsWith("PRE-"))
        {
            prematriculation = await _prematriculationService.GetByCodeAsync(searchTerm);
        }
        else
        {
            // Buscar por nombre de estudiante (simplificado, podría mejorarse con búsqueda más compleja)
            // Por ahora, buscar por código solo
            ModelState.AddModelError("", "Por favor ingrese un código de prematrícula válido");
            return View();
        }

        if (prematriculation == null)
        {
            ModelState.AddModelError("", "Prematrícula no encontrada");
            return View();
        }

        var payments = await _paymentService.GetByPrematriculationAsync(prematriculation.Id);
        
        ViewBag.Prematriculation = prematriculation;
        ViewBag.Payments = payments;

        return View("Register", new PaymentCreateDto 
        { 
            PrematriculationId = prematriculation.Id 
        });
    }

    // Registrar pago
    public async Task<IActionResult> Register(Guid prematriculationId)
    {
        var prematriculation = await _prematriculationService.GetByIdAsync(prematriculationId);
        if (prematriculation == null)
            return NotFound();

        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        ViewBag.Prematriculation = prematriculation;
        
        var payments = await _paymentService.GetByPrematriculationAsync(prematriculationId);
        ViewBag.Payments = payments;

        // Obtener conceptos de pago activos
        var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
        ViewBag.PaymentConcepts = paymentConcepts;

        return View(new PaymentCreateDto 
        { 
            PrematriculationId = prematriculationId,
            PaymentDate = DateTime.UtcNow
        });
    }

    [HttpPost]
    public async Task<IActionResult> Register(PaymentCreateDto dto, IFormFile? receiptImageFile)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var prematriculation = await _prematriculationService.GetByIdAsync(dto.PrematriculationId ?? Guid.Empty);
        if (prematriculation == null && dto.PrematriculationId.HasValue)
        {
            ModelState.AddModelError("", "Prematrícula no encontrada");
            return View(dto);
        }

        // Comprobantes: solo Cloudinary (cualquier ambiente).
        if (receiptImageFile != null && receiptImageFile.Length > 0)
        {
            try
            {
                if (!_cloudinaryService.IsConfigured)
                {
                    ModelState.AddModelError("ReceiptImage",
                        "Almacenamiento de comprobantes no disponible: configure Cloudinary (CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, CLOUDINARY_API_SECRET).");
                }
                else
                {
                    var imageUrl = await _cloudinaryService.UploadImageAsync(receiptImageFile, "payments/receipts");
                    if (!string.IsNullOrEmpty(imageUrl))
                        dto.ReceiptImage = imageUrl;
                    else
                        ModelState.AddModelError("ReceiptImage", "No se pudo subir el comprobante a Cloudinary. Intente de nuevo.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen del comprobante");
                ModelState.AddModelError("ReceiptImage", "Error al subir la imagen: " + ex.Message);
            }
        }

        // Validar que métodos manuales requieren comprobante
        if (!string.IsNullOrEmpty(dto.PaymentMethod) && 
            (dto.PaymentMethod == "Transferencia" || dto.PaymentMethod == "Depósito" || dto.PaymentMethod == "Yappy") &&
            string.IsNullOrEmpty(dto.ReceiptImage))
        {
            ModelState.AddModelError("ReceiptImage", "Los métodos de pago manuales requieren adjuntar un comprobante");
        }

        // MEJORADO: Validar y corregir fecha de pago si no es válida
        if (dto.PaymentDate == default(DateTime) || dto.PaymentDate == DateTime.MinValue || dto.PaymentDate.Year < 2000)
        {
            dto.PaymentDate = DateTime.UtcNow;
            _logger.LogWarning("Fecha de pago inválida en formulario, usando fecha actual");
        }

        if (!ModelState.IsValid)
        {
            if (prematriculation != null)
            {
                ViewBag.Prematriculation = prematriculation;
                var payments = await _paymentService.GetByPrematriculationAsync(prematriculation.Id);
                ViewBag.Payments = payments;
                var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
                ViewBag.PaymentConcepts = paymentConcepts;
            }
            return View(dto);
        }

        try
        {
            var payment = await _paymentService.CreateAsync(dto, currentUser.Id);
            
            TempData["SuccessMessage"] = $"Pago registrado exitosamente. Recibo: {payment.ReceiptNumber}";
            return RedirectToAction(nameof(Register), new { prematriculationId = dto.PrematriculationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar pago");
            ModelState.AddModelError("", "Error al registrar el pago: " + ex.Message);
            if (prematriculation != null)
            {
                ViewBag.Prematriculation = prematriculation;
                var payments = await _paymentService.GetByPrematriculationAsync(prematriculation.Id);
                ViewBag.Payments = payments;
                var paymentConcepts = await _paymentConceptService.GetActiveAsync(currentUser.SchoolId.Value);
                ViewBag.PaymentConcepts = paymentConcepts;
            }
            return View(dto);
        }
    }

    // Confirmar pago
    [HttpPost]
    [Authorize(Roles = "admin,superadmin,contabilidad,contable")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        try
        {
            var payment = await _paymentService.ConfirmPaymentAsync(id, currentUser.Id);
            
            // Enviar notificación al acudiente/estudiante sobre la confirmación del pago
            if (_messagingService != null && payment.StudentId.HasValue)
            {
                try
                {
                    var student = await _context.Users.FindAsync(payment.StudentId.Value);
                    if (student != null)
                    {
                        // Buscar el acudiente si existe
                        var prematriculation = await _prematriculationService.GetByIdAsync(payment.PrematriculationId);
                        Guid? recipientId = null;

                        if (prematriculation?.ParentId.HasValue == true)
                        {
                            recipientId = prematriculation.ParentId;
                        }
                        else if (payment.StudentId.HasValue)
                        {
                            recipientId = payment.StudentId;
                        }

                        if (recipientId.HasValue)
                        {
                            var studentName = student != null ? 
                                $"{student.Name} {student.LastName}" : "Estudiante";

                            var messageModel = new SchoolManager.ViewModels.SendMessageViewModel
                            {
                                RecipientType = "Individual",
                                RecipientId = recipientId.Value,
                                Subject = $"✅ Pago Confirmado - {studentName}",
                                Content = $@"
<h3>Pago Confirmado</h3>
<p>Estimado/a acudiente/estudiante,</p>
<p>Le informamos que el pago del recibo <strong>{payment.ReceiptNumber}</strong> ha sido confirmado exitosamente.</p>

<h4>Detalles del Pago:</h4>
<ul>
    <li><strong>Número de Recibo:</strong> {payment.ReceiptNumber}</li>
    <li><strong>Monto:</strong> {payment.Amount:C}</li>
    <li><strong>Fecha de Confirmación:</strong> {payment.ConfirmedAt?.ToString("dd/MM/yyyy HH:mm") ?? DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm")}</li>
</ul>

<p>Si este pago corresponde a una matrícula, la matrícula se activará automáticamente.</p>

<p>Puede consultar más detalles y descargar el recibo desde la plataforma.</p>
"
                            };

                            await _messagingService.SendMessageAsync(messageModel, currentUser.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar notificación de confirmación de pago");
                    // No fallar el proceso si la notificación falla
                }
            }
            
            TempData["SuccessMessage"] = $"Pago confirmado exitosamente. La matrícula se activará automáticamente si corresponde.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al confirmar pago");
            TempData["ErrorMessage"] = "Error al confirmar el pago: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // Ver detalles de un pago
    public async Task<IActionResult> Details(Guid id)
    {
        var payment = await _paymentService.GetByIdAsync(id);
        if (payment == null)
            return NotFound();

        if (!SchoolTenantHelper.CanAccessSchool(payment.SchoolId, _tenantContext))
            return Forbid();

        var payments = await _paymentService.GetByPrematriculationAsync(payment.PrematriculationId);
        var dto = payments.FirstOrDefault(p => p.Id == id);
        
        if (dto == null)
        {
            // Si no se encuentra por prematrícula, buscar directamente
            if (payment.StudentId.HasValue)
            {
                var studentPayments = await _paymentService.GetByStudentAsync(payment.StudentId.Value);
                dto = studentPayments.FirstOrDefault(p => p.Id == id);
            }
        }
        
        if (dto == null)
            return NotFound();

        return View(dto);
    }

    // Recibo PDF descargable
    [Authorize]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var payment = await _paymentService.GetByIdAsync(id);
        if (payment == null)
            return NotFound();

        // Verificar que el usuario tenga acceso
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var userRole = currentUser.Role?.ToLower() ?? "";
        var isAuthorized = userRole == "admin" || userRole == "superadmin" || 
                          userRole == "contable" || userRole == "contabilidad";

        if (!isAuthorized && payment.StudentId.HasValue)
        {
            if (userRole == "student" || userRole == "estudiante")
            {
                isAuthorized = payment.StudentId == currentUser.Id;
            }
            else
            {
                // Verificar si es acudiente del estudiante
                var prematriculation = await _prematriculationService.GetByIdAsync(payment.PrematriculationId);
                isAuthorized = prematriculation != null && prematriculation.ParentId == currentUser.Id;
            }
        }

        if (!isAuthorized)
            return Forbid();

        // Solo generar recibo si está confirmado
        if (payment.PaymentStatus != "Confirmado")
        {
            TempData["ErrorMessage"] = "Solo se puede generar el recibo para pagos confirmados";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Payment = payment;
        return View(payment);
    }

    // Vista para docente: seleccionar grupo para ver pagos
    [Authorize(Roles = "teacher,docente")]
    public async Task<IActionResult> SelectGroup()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        // Obtener grupos asignados al docente a través de sus asignaciones académicas
        var groups = await _context.TeacherAssignments
            .Where(ta => ta.TeacherId == currentUser.Id)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.Group)
            .Include(ta => ta.SubjectAssignment)
                .ThenInclude(sa => sa.GradeLevel)
            .Select(ta => new
            {
                Group = ta.SubjectAssignment.Group,
                GradeLevel = ta.SubjectAssignment.GradeLevel
            })
            .Where(x => x.Group != null)
            .Distinct()
            .Select(x => new
            {
                x.Group.Id,
                GroupName = x.Group.Name,
                GradeName = x.GradeLevel != null ? x.GradeLevel.Name : (x.Group.Grade ?? "Sin grado"),
                DisplayName = (x.GradeLevel != null ? x.GradeLevel.Name : (x.Group.Grade ?? "Sin grado")) + " - " + x.Group.Name
            })
            .ToListAsync();

        ViewBag.Groups = groups;
        return View();
    }

    // Vista para docente consultar pagos de su grupo
    [Authorize(Roles = "teacher,docente")]
    public async Task<IActionResult> ByGroup(Guid groupId)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        // Verificar que el docente tenga acceso al grupo a través de asignaciones académicas
        var group = await _context.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return NotFound();

        // Verificar si el docente tiene asignaciones académicas en este grupo
        var hasAccess = await _context.TeacherAssignments
            .Include(ta => ta.SubjectAssignment)
            .AnyAsync(ta => ta.TeacherId == currentUser.Id && 
                          ta.SubjectAssignment != null && 
                          ta.SubjectAssignment.GroupId == groupId);

        if (!hasAccess)
            return Forbid();

        var payments = await _paymentService.GetByGroupAsync(groupId);
        
        ViewBag.Group = group;
        return View(payments);
    }

    // Reportes de pagos
    [Authorize(Roles = "admin,superadmin,contabilidad,contable")]
    public async Task<IActionResult> Reports()
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        ViewBag.SchoolId = currentUser.SchoolId.Value;
        
        // Obtener conceptos de pago para filtro
        var concepts = await _paymentConceptService.GetAllAsync(currentUser.SchoolId.Value);
        ViewBag.PaymentConcepts = concepts;

        return View();
    }

    [HttpPost]
    [Authorize(Roles = "admin,superadmin,contabilidad,contable")]
    public async Task<IActionResult> GenerateReport(Guid? studentId, Guid? conceptId, DateTime? startDate, DateTime? endDate, string status)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser?.SchoolId == null)
            return Unauthorized();

        var payments = await _paymentService.GetBySchoolAsync(currentUser.SchoolId.Value);

        // Aplicar filtros
        if (studentId.HasValue)
        {
            payments = payments.Where(p => p.StudentId == studentId).ToList();
        }

        if (conceptId.HasValue)
        {
            payments = payments.Where(p => p.PaymentConceptId == conceptId).ToList();
        }

        if (startDate.HasValue)
        {
            payments = payments.Where(p => p.PaymentDate >= startDate.Value).ToList();
        }

        if (endDate.HasValue)
        {
            payments = payments.Where(p => p.PaymentDate <= endDate.Value.AddDays(1)).ToList();
        }

        if (!string.IsNullOrEmpty(status))
        {
            payments = payments.Where(p => p.PaymentStatus == status).ToList();
        }

        ViewBag.StudentId = studentId;
        ViewBag.ConceptId = conceptId;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.Status = status;

        return View("ReportResults", payments);
    }
}

