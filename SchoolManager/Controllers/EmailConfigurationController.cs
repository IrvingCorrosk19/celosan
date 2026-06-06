using Microsoft.AspNetCore.Mvc;
using SchoolManager.Dtos;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using SchoolManager.Helpers;

namespace SchoolManager.Controllers
{
    [Authorize(Roles = "superadmin,admin")]
    public class EmailConfigurationController : Controller
    {
        private readonly IEmailConfigurationService _emailConfigurationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<EmailConfigurationController> _logger;

        public EmailConfigurationController(
            IEmailConfigurationService emailConfigurationService,
            ICurrentUserService currentUserService,
            ILogger<EmailConfigurationController> logger)
        {
            _emailConfigurationService = emailConfigurationService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Iniciando método Index de EmailConfigurationController");
                
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                _logger.LogInformation("Usuario actual obtenido: {UserId}, Email: {Email}, SchoolId: {SchoolId}", 
                    currentUser?.Id, currentUser?.Email, currentUser?.SchoolId);
                
                if (currentUser?.SchoolId == null)
                {
                    _logger.LogWarning("No se pudo obtener la información de la escuela del usuario actual");
                    TempData["ErrorMessage"] = "No se pudo obtener la información de la escuela del usuario actual.";
                    return View(new List<EmailConfigurationDto>());
                }

                _logger.LogInformation("Buscando configuración de email para SchoolId: {SchoolId}", currentUser.SchoolId.Value);
                var emailConfig = await _emailConfigurationService.GetBySchoolIdAsync(currentUser.SchoolId.Value);
                
                if (emailConfig != null)
                {
                    _logger.LogInformation("Configuración de email encontrada: {ConfigId}, SMTP Server: {SmtpServer}", 
                        emailConfig.Id, emailConfig.SmtpServer);
                }
                else
                {
                    _logger.LogInformation("No se encontró configuración de email para SchoolId: {SchoolId}", currentUser.SchoolId.Value);
                }

                var emailConfigs = emailConfig != null ? new List<EmailConfigurationDto> { emailConfig } : new List<EmailConfigurationDto>();
                _logger.LogInformation("Retornando vista con {Count} configuraciones de email", emailConfigs.Count);
                
                return View(emailConfigs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en método Index de EmailConfigurationController");
                TempData["ErrorMessage"] = "Ocurrió un error al cargar las configuraciones de email.";
                return View(new List<EmailConfigurationDto>());
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var configuration = await _emailConfigurationService.GetByIdAsync(id);
            if (configuration == null)
            {
                return NotFound();
            }

            return View(configuration);
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                _logger.LogInformation("Iniciando método Create (GET) de EmailConfigurationController");
                
                var currentUser = await _currentUserService.GetCurrentUserAsync();
                _logger.LogInformation("Usuario actual obtenido: {UserId}, Email: {Email}, SchoolId: {SchoolId}", 
                    currentUser?.Id, currentUser?.Email, currentUser?.SchoolId);
                
                if (currentUser?.SchoolId == null)
                {
                    _logger.LogWarning("No se pudo obtener la información de la escuela del usuario actual");
                    TempData["ErrorMessage"] = "No se pudo obtener la información de la escuela del usuario actual.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar si ya existe una configuración para esta escuela
                _logger.LogInformation("Verificando si ya existe configuración para SchoolId: {SchoolId}", currentUser.SchoolId.Value);
                var existingConfig = await _emailConfigurationService.GetBySchoolIdAsync(currentUser.SchoolId.Value);
                
                if (existingConfig != null)
                {
                    _logger.LogInformation("Ya existe configuración de email: {ConfigId}, redirigiendo a Edit", existingConfig.Id);
                    TempData["WarningMessage"] = "Ya existe una configuración de correo para esta escuela. Edítala si deseas cambiarla.";
                    return RedirectToAction(nameof(Edit), new { id = existingConfig.Id });
                }

                _logger.LogInformation("No existe configuración previa, mostrando formulario de creación");
                return View(new EmailConfigurationCreateDto { SchoolId = currentUser.SchoolId.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en método Create (GET) de EmailConfigurationController");
                TempData["ErrorMessage"] = "Ocurrió un error al cargar el formulario de creación.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmailConfigurationCreateDto model)
        {
            try
            {
                _logger.LogInformation("Iniciando método Create (POST) de EmailConfigurationController");
                _logger.LogInformation("Datos recibidos - SchoolId: {SchoolId}, SmtpServer: {SmtpServer}, SmtpPort: {SmtpPort}, FromEmail: {FromEmail}", 
                    model.SchoolId, model.SmtpServer, model.SmtpPort, model.FromEmail);
                
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState no es válido. Errores: {Errors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return View(model);
                }

                var currentUser = await _currentUserService.GetCurrentUserAsync();
                if (currentUser?.SchoolId == null || model.SchoolId != currentUser.SchoolId.Value)
                {
                    TempData["ErrorMessage"] = "No puede crear configuración para otra escuela.";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogInformation("Creando configuración de email para SchoolId: {SchoolId}", model.SchoolId);
                var createdConfig = await _emailConfigurationService.CreateAsync(model);
                _logger.LogInformation("Configuración de email creada exitosamente con ID: {ConfigId}", createdConfig.Id);
                
                TempData["SuccessMessage"] = "Configuración de email creada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de email para SchoolId: {SchoolId}", model.SchoolId);
                ModelState.AddModelError("", $"Error al crear la configuración: {ex.Message}");
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var configuration = await _emailConfigurationService.GetByIdAsync(id);
            if (configuration == null)
            {
                return NotFound();
            }

            var updateDto = new EmailConfigurationUpdateDto
            {
                Id = configuration.Id,
                SmtpServer = configuration.SmtpServer,
                SmtpPort = configuration.SmtpPort,
                SmtpUsername = configuration.SmtpUsername,
                SmtpPassword = configuration.SmtpPassword,
                SmtpUseSsl = configuration.SmtpUseSsl,
                SmtpUseTls = configuration.SmtpUseTls,
                FromEmail = configuration.FromEmail,
                FromName = configuration.FromName,
                IsActive = configuration.IsActive
            };

            return View(updateDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, EmailConfigurationUpdateDto model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _emailConfigurationService.UpdateAsync(model);
                TempData["SuccessMessage"] = "Configuración de email actualizada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error al actualizar la configuración: {ex.Message}");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _emailConfigurationService.DeleteAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "Configuración de email eliminada exitosamente.";
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo eliminar la configuración de email.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al eliminar la configuración: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            try
            {
                _logger.LogInformation("Iniciando prueba de conexión para configuración ID: {ConfigId}", id);
                var result = await _emailConfigurationService.TestConnectionAsync(id);
                _logger.LogInformation("Resultado de prueba de conexión para ID {ConfigId}: {Result}", id, result);
                return Json(new { success = result, message = result ? "Conexión exitosa" : "Error en la conexión" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al probar conexión para configuración ID: {ConfigId}", id);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestConnectionBySchool(Guid schoolId)
        {
            try
            {
                var result = await _emailConfigurationService.TestConnectionBySchoolIdAsync(schoolId);
                return Json(new { success = result, message = result ? "Conexión exitosa" : "Error en la conexión" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}
