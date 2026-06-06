using Microsoft.Extensions.Options;
using SchoolManager.Options;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class NocturnalEnrollmentSettingsService : INocturnalEnrollmentSettingsService
{
    private readonly NocturnalAdvancedEnrollmentOptions _options;
    private readonly ICurrentUserService _currentUserService;

    public NocturnalEnrollmentSettingsService(
        IOptions<NocturnalAdvancedEnrollmentOptions> options,
        ICurrentUserService currentUserService)
    {
        _options = options.Value;
        _currentUserService = currentUserService;
    }

    public bool IsAdvancedEnabled(Guid? schoolId)
    {
        if (_options.EnableForAllSchools)
            return true;

        if (!schoolId.HasValue || schoolId.Value == Guid.Empty)
            return false;

        return _options.EnabledSchoolIds.Any(id =>
            Guid.TryParse(id, out var g) && g == schoolId.Value);
    }

    public async Task<bool> IsAdvancedEnabledForCurrentSchoolAsync()
    {
        var user = await _currentUserService.GetCurrentUserAsync();
        return IsAdvancedEnabled(user?.SchoolId);
    }
}
