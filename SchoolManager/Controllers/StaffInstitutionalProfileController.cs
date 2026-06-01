using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManager.Helpers;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;

namespace SchoolManager.Controllers;

[Authorize(Roles = StaffInstitutionalProfileAccess.AuthorizeRoles)]
[Route("StaffInstitutionalProfile")]
public class StaffInstitutionalProfileController : Controller
{
    private readonly IStaffInstitutionalProfileService _profileService;
    private readonly IUserPhotoService _userPhotoService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<StaffInstitutionalProfileController> _logger;

    public StaffInstitutionalProfileController(
        IStaffInstitutionalProfileService profileService,
        IUserPhotoService userPhotoService,
        ICurrentUserService currentUserService,
        ILogger<StaffInstitutionalProfileController> logger)
    {
        _profileService = profileService;
        _userPhotoService = userPhotoService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var role = await _currentUserService.GetCurrentUserRoleAsync();
        if (!StaffInstitutionalProfileAccess.IsAllowedRole(role))
            return Forbid();

        var profile = await _profileService.GetProfileAsync(currentUserId.Value);
        if (profile == null)
        {
            TempData["Error"] = "No se pudo cargar tu perfil institucional. Contacta al administrador.";
            return RedirectToAction("Index", "Home");
        }

        return View(profile);
    }

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(StaffInstitutionalProfileViewModel model)
    {
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue || currentUserId.Value != model.Id)
            return Unauthorized();

        var role = await _currentUserService.GetCurrentUserRoleAsync();
        if (!StaffInstitutionalProfileAccess.IsAllowedRole(role))
            return Forbid();

        async Task<IActionResult> ReturnIndexWithModelAsync()
        {
            var refreshed = await _profileService.GetProfileAsync(model.Id);
            if (refreshed != null)
            {
                refreshed.Name = model.Name;
                refreshed.LastName = model.LastName;
                refreshed.Email = model.Email;
                refreshed.DocumentId = model.DocumentId;
                refreshed.DateOfBirth = model.DateOfBirth;
                refreshed.CellphonePrimary = model.CellphonePrimary;
                refreshed.CellphoneSecondary = model.CellphoneSecondary;
                refreshed.BloodType = model.BloodType;
                refreshed.Allergies = model.Allergies;
                refreshed.EmergencyContactName = model.EmergencyContactName;
                refreshed.EmergencyContactPhone = model.EmergencyContactPhone;
                refreshed.EmergencyRelationship = model.EmergencyRelationship;
                refreshed.JobTitle = model.JobTitle;
                refreshed.Department = model.Department;
                refreshed.EmployeeCode = model.EmployeeCode;
                return View("Index", refreshed);
            }
            return View("Index", model);
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Por favor, corrige los errores en el formulario.";
            return await ReturnIndexWithModelAsync();
        }

        if (!await _profileService.IsEmailAvailableAsync(model.Email, model.Id))
        {
            ModelState.AddModelError("Email", "Este correo electrónico ya está registrado por otro usuario.");
            TempData["Error"] = "El correo electrónico ya está en uso.";
            return await ReturnIndexWithModelAsync();
        }

        if (!string.IsNullOrEmpty(model.DocumentId) &&
            !await _profileService.IsDocumentIdAvailableAsync(model.DocumentId, model.Id))
        {
            ModelState.AddModelError("DocumentId", "Este documento ya está registrado por otro usuario.");
            TempData["Error"] = "El documento de identidad ya está en uso.";
            return await ReturnIndexWithModelAsync();
        }

        var success = await _profileService.UpdateProfileAsync(model, currentUserId.Value);
        if (success)
        {
            TempData["Success"] = "Tu perfil institucional ha sido actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = "No se pudo actualizar tu perfil. Por favor, intenta nuevamente.";
        return await ReturnIndexWithModelAsync();
    }

    [HttpGet("CheckEmailAvailability")]
    public async Task<IActionResult> CheckEmailAvailability(string email, Guid userId)
    {
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue || currentUserId.Value != userId)
            return Unauthorized();

        var available = await _profileService.IsEmailAvailableAsync(email, userId);
        return Json(new { available });
    }

    [HttpGet("CheckDocumentAvailability")]
    public async Task<IActionResult> CheckDocumentAvailability(string documentId, Guid userId)
    {
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue || currentUserId.Value != userId)
            return Unauthorized();

        var available = await _profileService.IsDocumentIdAvailableAsync(documentId, userId);
        return Json(new { available });
    }

    [HttpPost("UpdatePhoto")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UpdatePhoto(IFormFile? photo)
    {
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var role = await _currentUserService.GetCurrentUserRoleAsync();
        if (!StaffInstitutionalProfileAccess.IsAllowedRole(role))
            return Forbid();

        if (photo == null || photo.Length == 0)
        {
            TempData["Error"] = "Seleccione una imagen (JPEG o PNG; si supera 2 MB se comprimirá automáticamente, máx. de subida 12 MB).";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _userPhotoService.UpdatePhotoAsync(currentUserId.Value, photo);
            TempData["Success"] = "Foto actualizada correctamente.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffProfile] Error actualizando foto");
            TempData["Error"] = "No se pudo actualizar la foto. Use JPEG o PNG (máx. de subida 12 MB).";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("RemovePhoto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePhoto()
    {
        var currentUserId = await _currentUserService.GetCurrentUserIdAsync();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var role = await _currentUserService.GetCurrentUserRoleAsync();
        if (!StaffInstitutionalProfileAccess.IsAllowedRole(role))
            return Forbid();

        try
        {
            await _userPhotoService.RemovePhotoAsync(currentUserId.Value);
            TempData["Success"] = "Foto eliminada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffProfile] Error eliminando foto");
            TempData["Error"] = "No se pudo eliminar la foto.";
        }

        return RedirectToAction(nameof(Index));
    }
}
