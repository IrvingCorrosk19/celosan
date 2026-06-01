using SchoolManager.ViewModels;

namespace SchoolManager.Services.Interfaces;

public interface IStaffInstitutionalProfileService
{
    Task<StaffInstitutionalProfileViewModel?> GetProfileAsync(Guid userId);

    Task<bool> UpdateProfileAsync(StaffInstitutionalProfileViewModel model, Guid actorId);

    Task<bool> IsEmailAvailableAsync(string email, Guid currentUserId);

    Task<bool> IsDocumentIdAvailableAsync(string? documentId, Guid currentUserId);

    Task EnsureStaffProfileRowAsync(Guid userId);
}
