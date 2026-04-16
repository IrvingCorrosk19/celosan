using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;
using SchoolManager.Options;
using Microsoft.Extensions.Options;

namespace SchoolManager.Services.Implementations;

public class StudentIdCardService : IStudentIdCardService
{
    private readonly SchoolDbContext _context;
    private readonly ILogger<StudentIdCardService> _logger;
    private readonly IQrSignatureService _qrSignatureService;
    private readonly IOptions<StudentIdCardOptions> _cardOptions;

    /// <summary>
    /// Vida del token QR. Constante pública para que PdfService use el mismo valor.
    /// Un carnet se emite por 1 año; el token dura 6 meses para forzar renovación semestral.
    /// </summary>
    public const int QrTokenValidityMonths = 6;

    /// <summary>Valores permitidos para ScanType. Protege contra inyección de datos en scan_logs.</summary>
    private static readonly HashSet<string> AllowedScanTypes =
        new(StringComparer.OrdinalIgnoreCase) { "entry", "exit", "event", "cafeteria" };

    public StudentIdCardService(
        SchoolDbContext context,
        ILogger<StudentIdCardService> logger,
        IQrSignatureService qrSignatureService,
        IOptions<StudentIdCardOptions> cardOptions)
    {
        _context = context;
        _logger = logger;
        _qrSignatureService = qrSignatureService;
        _cardOptions = cardOptions;
    }

    private string? ResolveSiteBaseUrl(string? siteBaseUrlOverride)
    {
        if (!string.IsNullOrWhiteSpace(siteBaseUrlOverride))
            return siteBaseUrlOverride.TrimEnd('/');
        var o = _cardOptions.Value.PublicBaseUrl;
        return string.IsNullOrWhiteSpace(o) ? null : o.TrimEnd('/');
    }

    private string? BuildEmergencyQrDataUrl(Guid studentId, string? siteBaseUrlOverride)
    {
        var baseUrl = ResolveSiteBaseUrl(siteBaseUrlOverride);
        var url = CarnetEmergencyInfoLink.BuildPublicUrl(baseUrl, studentId, _qrSignatureService);
        if (url == null)
            return null;
        var png = QrHelper.GenerateQrPng(url, null);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetCurrentCardAsync — solo lectura, nunca modifica estado
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<StudentIdCardDto?> GetCurrentCardAsync(Guid studentId, string? siteBaseUrl = null)
    {
        var row = await StudentRoleFilter.WhereIsStudent(_context.Users.AsNoTracking())
            .Where(x => x.Id == studentId)
            .Select(x => new
            {
                x.Name,
                x.LastName,
                x.PhotoUrl,
                HasActiveAssignment = x.StudentAssignments.Any(a => a.IsActive),
                Grade = x.StudentAssignments.Where(a => a.IsActive)
                    .OrderByDescending(a => a.EnrollmentType != null && a.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(a => a.Shift != null && a.Shift.Name != null && a.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                    .Select(a => a.Grade.Name)
                    .FirstOrDefault(),
                Group = x.StudentAssignments.Where(a => a.IsActive)
                    .OrderByDescending(a => a.EnrollmentType != null && a.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(a => a.Shift != null && a.Shift.Name != null && a.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                    .Select(a => a.Group.Name)
                    .FirstOrDefault(),
                ShiftName = x.StudentAssignments.Where(a => a.IsActive)
                    .OrderByDescending(a => a.EnrollmentType != null && a.EnrollmentType.ToLower() == "nocturno")
                    .ThenByDescending(a => a.Shift != null && a.Shift.Name != null && a.Shift.Name.ToLower().Contains("noche"))
                    .ThenByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                    .Select(a => a.Shift != null ? a.Shift.Name : null)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (row == null || !row.HasActiveAssignment)
            return null;

        var card = await _context.StudentIdCards
            .AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Status == "active")
            .Select(x => new { x.CardNumber })
            .FirstOrDefaultAsync();

        if (card == null)
            return null;

        var token = await _context.StudentQrTokens
            .AsNoTracking()
            .Where(x => x.StudentId == studentId && !x.IsRevoked &&
                (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
            .Select(x => new { x.Token })
            .FirstOrDefaultAsync();

        string? qrImageDataUrl = null;
        if (token != null)
        {
            var pngBytes = QrHelper.GenerateQrPng(token.Token, _qrSignatureService);
            qrImageDataUrl = "data:image/png;base64," + Convert.ToBase64String(pngBytes);
        }

        return new StudentIdCardDto
        {
            StudentId = studentId,
            CardNumber = card.CardNumber,
            FullName = $"{row.Name} {row.LastName}",
            Grade = row.Grade ?? "",
            Group = row.Group ?? "",
            Shift = string.IsNullOrEmpty(row.ShiftName) ? "N/A" : row.ShiftName,
            QrToken = token?.Token ?? "",
            QrImageDataUrl = qrImageDataUrl,
            EmergencyInfoQrImageDataUrl = BuildEmergencyQrDataUrl(studentId, siteBaseUrl),
            PhotoUrl = row.PhotoUrl
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GenerateAsync — crea nuevo carnet revocando el anterior
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<StudentIdCardDto> GenerateAsync(Guid studentId, Guid createdBy, string? siteBaseUrl = null)
    {
        _logger.LogInformation(
            "[StudentIdCard] GenerateAsync inicio StudentId={StudentId} CreatedBy={CreatedBy}",
            studentId, createdBy);

        // CRÍTICO-2: Transacción serializable — previene race condition con generaciones concurrentes
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var student = await StudentRoleFilter.WhereIsStudent(_context.Users)
                .Include(x => x.StudentAssignments.Where(a => a.IsActive))
                    .ThenInclude(x => x.Grade)
                .Include(x => x.StudentAssignments.Where(a => a.IsActive))
                    .ThenInclude(x => x.Group)
                .Include(x => x.StudentAssignments.Where(a => a.IsActive))
                    .ThenInclude(x => x.Shift)
                .FirstOrDefaultAsync(x => x.Id == studentId);

            if (student == null)
            {
                _logger.LogWarning(
                    "[StudentIdCard] GenerateAsync estudiante no encontrado StudentId={StudentId}", studentId);
                throw new Exception("Estudiante no encontrado");
            }

            // PAY-GATE dentro de la transacción serializable — atomic check + write
            var payment = await _context.StudentPaymentAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudentId == studentId);

            if (payment == null || payment.CarnetStatus != "Pagado")
            {
                _logger.LogWarning(
                    "[StudentIdCard] GenerateAsync denegado: CarnetStatus={Status} StudentId={StudentId}",
                    payment?.CarnetStatus ?? "sin registro", studentId);
                throw new Exception("El estudiante no ha pagado el carnet.");
            }

            var activeAssignment = ActiveStudentAssignmentHelper.PickForDisplay(student.StudentAssignments);
            if (activeAssignment == null)
            {
                _logger.LogWarning(
                    "[StudentIdCard] GenerateAsync estudiante sin asignación activa StudentId={StudentId}", studentId);
                throw new Exception("Estudiante sin asignación activa");
            }

            // Revocar TODOS los carnets activos (cubre duplicados de race conditions previas)
            var existingCards = await _context.StudentIdCards
                .Where(x => x.StudentId == studentId && x.Status == "active")
                .ToListAsync();

            foreach (var ec in existingCards)
            {
                _logger.LogInformation(
                    "[StudentIdCard] Revocando carnet anterior Id={CardId} CardNumber={CardNumber}",
                    ec.Id, ec.CardNumber);
                ec.Status = "revoked";
            }

            // Revocar TODOS los tokens QR activos
            var existingTokens = await _context.StudentQrTokens
                .Where(x => x.StudentId == studentId && !x.IsRevoked)
                .ToListAsync();

            foreach (var et in existingTokens)
                et.IsRevoked = true;

            // LÓGICA-5 fix: usar CardNumberHelper centralizado (no más código duplicado)
            var cardNumber = CardNumberHelper.Generate(studentId);
            _logger.LogInformation(
                "[StudentIdCard] Nuevo carnet CardNumber={CardNumber} StudentId={StudentId}",
                cardNumber, studentId);

            var card = new StudentIdCard
            {
                StudentId = studentId,
                CardNumber = cardNumber,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Status = "active"
            };

            // LÓGICA-6 fix: formato GUID sin guiones ("N") — consistente con PdfService
            // LÓGICA-3 fix: usar constante QrTokenValidityMonths (6 meses, igual que PdfService)
            var newToken = new StudentQrToken
            {
                StudentId = studentId,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddMonths(QrTokenValidityMonths)
            };

            _context.StudentIdCards.Add(card);
            _context.StudentQrTokens.Add(newToken);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "[StudentIdCard] GenerateAsync OK StudentId={StudentId} CardNumber={CardNumber}",
                studentId, cardNumber);

            // Generar QR como data URI en el servidor para evitar dependencias de CDN en la vista
            var pngBytes = QrHelper.GenerateQrPng(newToken.Token, _qrSignatureService);
            var qrImageDataUrl = "data:image/png;base64," + Convert.ToBase64String(pngBytes);

            return new StudentIdCardDto
            {
                StudentId = studentId,
                CardNumber = cardNumber,
                FullName = $"{student.Name} {student.LastName}",
                Grade = activeAssignment.Grade?.Name ?? "",
                Group = activeAssignment.Group?.Name ?? "",
                Shift = activeAssignment.Shift?.Name ?? "N/A",
                QrToken = newToken.Token,
                QrImageDataUrl = qrImageDataUrl,
                EmergencyInfoQrImageDataUrl = BuildEmergencyQrDataUrl(studentId, siteBaseUrl),
                PhotoUrl = student.PhotoUrl
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ScanAsync — valida QR y devuelve datos del estudiante
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ScanResultDto> ScanAsync(ScanRequestDto request)
    {
        // SEG-3: Normalizar ScanType — rechazar valores no permitidos
        var scanType = AllowedScanTypes.Contains(request.ScanType ?? "") ? request.ScanType : "entry";

        var tokenToLookup = request.Token;

        // Token firmado: validar firma HMAC antes de buscar en BD
        if (request.Token.Contains("|"))
        {
            if (!_qrSignatureService.ValidateSignedToken(request.Token))
            {
                await SaveScanLogAsync(null, scanType, "denied", request.ScannedBy);
                return DeniedResult("QR inválido o expirado");
            }
            tokenToLookup = _qrSignatureService.ExtractTokenFromSigned(request.Token) ?? request.Token;
        }

        // SCALE-1: Cargar solo datos esenciales; asignaciones en query separada
        var tokenRecord = await _context.StudentQrTokens
            .Include(x => x.Student)
                .ThenInclude(x => x.SchoolNavigation)
            .FirstOrDefaultAsync(x =>
                x.Token == tokenToLookup &&
                !x.IsRevoked &&
                (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow));

        if (tokenRecord == null)
        {
            await SaveScanLogAsync(null, scanType, "denied", request.ScannedBy);
            return DeniedResult("QR inválido o expirado");
        }

        // BUG-1: Verificar que el estudiante no fue eliminado (FK sin cascade delete)
        if (tokenRecord.Student == null)
        {
            _logger.LogWarning(
                "[StudentIdCard] ScanAsync token sin estudiante asociado TokenId={TokenId}", tokenRecord.Id);
            await SaveScanLogAsync(tokenRecord.StudentId, scanType, "denied", request.ScannedBy);
            return DeniedResult("Estudiante no encontrado");
        }

        // SCALE-1: Query separada y filtrada para obtener solo la asignación activa con Grade/Group
        var assignmentCandidates = await _context.StudentAssignments
            .Include(a => a.Grade)
            .Include(a => a.Group)
            .Include(a => a.Shift)
            .AsNoTracking()
            .Where(a => a.StudentId == tokenRecord.StudentId && a.IsActive)
            .ToListAsync();
        var assignment = ActiveStudentAssignmentHelper.PickForDisplay(assignmentCandidates);

        var studentAccountActive = string.Equals(
            tokenRecord.Student.Status?.Trim(),
            "active",
            StringComparison.OrdinalIgnoreCase);

        if (assignment == null)
        {
            await SaveScanLogAsync(tokenRecord.StudentId, scanType, "denied", request.ScannedBy);
            return new ScanResultDto
            {
                Allowed = false,
                Message = "Estudiante sin asignación activa",
                StudentName = $"{tokenRecord.Student.Name} {tokenRecord.Student.LastName}",
                Grade = "N/A",
                Group = "N/A",
                DisciplineCount = 0,
                AllowedToEnterSchool = false,
                IsStudentAccountActive = studentAccountActive,
                ShiftName = string.IsNullOrWhiteSpace(tokenRecord.Student.Shift)
                    ? null
                    : tokenRecord.Student.Shift.Trim(),
                CounselorName = null
            };
        }

        // Cargar carnet activo
        var card = await _context.StudentIdCards
            .AsNoTracking()
            .Where(c => c.StudentId == tokenRecord.StudentId && c.Status == "active")
            .FirstOrDefaultAsync();

        // LÓGICA-2 fix: verificar que el carnet no esté vencido por fecha
        var cardExpired = card?.ExpiresAt.HasValue == true && card.ExpiresAt < DateTime.UtcNow;

        // LÓGICA-1 fix: calcular AllowedToEnterSchool ANTES de guardar el ScanLog
        // El ScanLog debe reflejar la decisión operativa real, no solo la validez del token
        var allowedToEnterSchool =
            tokenRecord.Student.Status == "active"
            && card != null
            && card.Status == "active"
            && !cardExpired;

        // LÓGICA-1 fix: ScanLog.Result refleja allowedToEnterSchool (no "allowed" siempre que el token sea válido)
        await SaveScanLogAsync(
            tokenRecord.StudentId,
            scanType,
            allowedToEnterSchool ? "allowed" : "denied",
            request.ScannedBy);

        // SCALE-3: Count de disciplina (requiere índice en discipline_reports.student_id)
        var disciplineCount = await _context.DisciplineReports
            .AsNoTracking()
            .Where(r => r.StudentId == tokenRecord.StudentId)
            .CountAsync();

        // CRÍTICO-1 fix: usar AuthenticatedRole del JWT, NUNCA el rol de ScannedBy en el body
        // AuthenticatedRole es poblado por el controller desde ClaimTypes.Role tras pasar por
        // la autenticación (Cookie o ApiBearerTokenMiddleware para la APK)
        var role = (request.AuthenticatedRole ?? "").Trim().ToLowerInvariant();
        var canSeeSensitiveData = role is "inspector" or "teacher" or "docente" or "admin" or "superadmin";

        var shiftDisplay = assignment.Shift?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(shiftDisplay))
            shiftDisplay = string.IsNullOrWhiteSpace(tokenRecord.Student.Shift)
                ? null
                : tokenRecord.Student.Shift.Trim();

        string? counselorName = null;
        if (tokenRecord.Student.SchoolId.HasValue)
        {
            counselorName = await ResolveCounselorFullNameAsync(
                tokenRecord.Student.SchoolId.Value,
                assignment.GradeId,
                assignment.GroupId);
        }

        // LÓGICA-7 fix: Allowed = AllowedToEnterSchool (decisión operativa real)
        return new ScanResultDto
        {
            Allowed = allowedToEnterSchool,
            Message = allowedToEnterSchool ? "Acceso permitido" : "Acceso denegado",
            StudentName = $"{tokenRecord.Student.Name} {tokenRecord.Student.LastName}",
            Grade = assignment.Grade?.Name ?? "N/A",
            Group = assignment.Group?.Name ?? "N/A",
            StudentId = tokenRecord.StudentId,
            DisciplineCount = disciplineCount,
            StudentPhotoUrl = tokenRecord.Student.PhotoUrl,
            SchoolName = tokenRecord.Student.SchoolNavigation?.Name,
            // SEG-1 fix: DocumentId (cédula del menor) solo visible para roles autorizados
            StudentCode = canSeeSensitiveData ? tokenRecord.Student.DocumentId : null,
            EmergencyContactName = canSeeSensitiveData ? tokenRecord.Student.EmergencyContactName : null,
            EmergencyContactPhone = canSeeSensitiveData ? tokenRecord.Student.EmergencyContactPhone : null,
            Allergies = canSeeSensitiveData ? tokenRecord.Student.Allergies : null,
            CardNumber = card?.CardNumber,
            // LÓGICA-2 fix: exponer "expired" cuando el carnet está vencido por fecha
            CardStatus = cardExpired ? "expired" : card?.Status,
            CardIssuedDate = card?.IssuedAt,
            AllowedToEnterSchool = allowedToEnterSchool,
            ShiftName = shiftDisplay,
            CounselorName = counselorName,
            IsStudentAccountActive = studentAccountActive
        };
    }

    /// <summary>
    /// Consejero por prioridad: asignación por grupo → por grado (sin grupo) → consejero general de la escuela.
    /// </summary>
    private async Task<string?> ResolveCounselorFullNameAsync(Guid schoolId, Guid gradeId, Guid groupId)
    {
        var byGroup = await _context.CounselorAssignments.AsNoTracking()
            .Where(ca => ca.SchoolId == schoolId && ca.GroupId == groupId && ca.IsActive && ca.IsCounselor)
            .Select(ca => ca.User.Name + " " + ca.User.LastName)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(byGroup))
            return byGroup.Trim();

        var byGrade = await _context.CounselorAssignments.AsNoTracking()
            .Where(ca => ca.SchoolId == schoolId && ca.GradeId == gradeId && ca.GroupId == null && ca.IsActive &&
                         ca.IsCounselor)
            .Select(ca => ca.User.Name + " " + ca.User.LastName)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(byGrade))
            return byGrade.Trim();

        var general = await _context.CounselorAssignments.AsNoTracking()
            .Where(ca => ca.SchoolId == schoolId && ca.GradeId == null && ca.GroupId == null && ca.IsActive &&
                         ca.IsCounselor)
            .Select(ca => ca.User.Name + " " + ca.User.LastName)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(general) ? null : general.Trim();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ──────────────────────────────────────────────────────────────────────────

    private static ScanResultDto DeniedResult(string message) => new()
    {
        Allowed = false,
        Message = message,
        StudentName = "N/A",
        Grade = "N/A",
        Group = "N/A",
        DisciplineCount = 0,
        AllowedToEnterSchool = false,
        IsStudentAccountActive = false,
        ShiftName = null,
        CounselorName = null
    };

    /// <summary>
    /// Guarda el ScanLog con manejo de error aislado.
    /// Un fallo de log nunca debe bloquear la respuesta al escáner.
    /// </summary>
    private async Task SaveScanLogAsync(Guid? studentId, string scanType, string result, Guid scannedBy)
    {
        try
        {
            _context.ScanLogs.Add(new ScanLog
            {
                StudentId = studentId,
                ScanType = scanType,
                Result = result,
                ScannedBy = scannedBy
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[StudentIdCard] Error guardando ScanLog StudentId={StudentId} Result={Result}",
                studentId, result);
            // No re-lanzar: el scan fue procesado, el log es secundario
        }
    }
}
