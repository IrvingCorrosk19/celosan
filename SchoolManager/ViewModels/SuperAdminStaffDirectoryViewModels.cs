using Microsoft.AspNetCore.Mvc.Rendering;

namespace SchoolManager.ViewModels;

public class SuperAdminStaffDirectoryFilterVm
{
    public string? Search { get; set; }
    public Guid? SchoolId { get; set; }

    /// <summary>Filtro por rol exacto en BD (opcional).</summary>
    public string? Role { get; set; }

    public string? UserStatus { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class SuperAdminStaffDirectoryRowVm
{
    public Guid UserId { get; set; }
    public string? PhotoUrl { get; set; }
    public string FullName { get; set; } = "";
    public string? DocumentId { get; set; }
    public string Email { get; set; } = "";
    public string? SchoolName { get; set; }
    public Guid? SchoolId { get; set; }
    public string RoleRaw { get; set; } = "";
    public string RoleDisplay { get; set; } = "";
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? EmployeeCode { get; set; }
    public string Status { get; set; } = "";
}

public class SuperAdminStaffDirectoryPageVm
{
    public SuperAdminStaffDirectoryFilterVm Filter { get; set; } = new();
    public List<SuperAdminStaffDirectoryRowVm> Rows { get; set; } = new();
    public List<SelectListItem> SchoolOptions { get; set; } = new();
    public List<SelectListItem> RoleOptions { get; set; } = new();

    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    public int DisplayFrom => TotalCount == 0 ? 0 : (Filter.Page - 1) * Filter.PageSize + 1;
    public int DisplayTo => TotalCount == 0 ? 0 : Math.Min(Filter.Page * Filter.PageSize, TotalCount);

    public Dictionary<string, string> GetDirectoryQueryForPage(int page)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Page"] = Math.Max(1, page).ToString(),
            ["PageSize"] = Filter.PageSize.ToString()
        };
        if (!string.IsNullOrWhiteSpace(Filter.Search))
            d["Search"] = Filter.Search;
        if (Filter.SchoolId.HasValue)
            d["SchoolId"] = Filter.SchoolId.Value.ToString("D");
        if (!string.IsNullOrWhiteSpace(Filter.Role))
            d["Role"] = Filter.Role;
        if (!string.IsNullOrEmpty(Filter.UserStatus))
            d["UserStatus"] = Filter.UserStatus!;
        return d;
    }
}
