namespace SchoolManager.Services.Interfaces;

public interface INocturnalEnrollmentSettingsService
{
    bool IsAdvancedEnabled(Guid? schoolId);

    Task<bool> IsAdvancedEnabledForCurrentSchoolAsync();
}
