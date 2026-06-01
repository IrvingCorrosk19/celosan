using SchoolManager.Dtos;
using SchoolManager.ViewModels;

namespace SchoolManager.Services.Interfaces;

public interface IInstitutionalCredentialService
{
    Task<InstitutionalCredentialCardDto?> GetCurrentCardAsync(Guid userId, string? siteBaseUrl = null);

    Task<InstitutionalCredentialCardDto> GenerateAsync(Guid userId, Guid createdBy);

    /// <summary>Resuelve perfil público a partir del token almacenado en staff_qr_tokens (sin exponer UserId).</summary>
    Task<StaffMemberPublicProfileVm?> ResolvePublicProfileByQrTokenAsync(string rawQrToken);
}
