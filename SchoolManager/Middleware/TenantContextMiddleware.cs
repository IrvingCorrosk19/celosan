using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SchoolManager.Helpers;
using SchoolManager.Services.Implementations;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Middleware;

/// <summary>Resuelve escuela del usuario autenticado para filtros multi-tenant.</summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User?.Identity?.IsAuthenticated == true && tenantContext is TenantContext tenant)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
            tenant.BypassTenantFilter = SchoolTenantHelper.IsSuperAdminRole(role);

            var schoolClaim = context.User.FindFirst(TenantClaimTypes.SchoolId)?.Value;
            if (Guid.TryParse(schoolClaim, out var schoolId))
                tenant.SchoolId = schoolId;
        }

        await _next(context);
    }
}
