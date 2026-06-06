using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SchoolManager.Services.Implementations
{
    public class UserPasswordManagementService : IUserPasswordManagementService
    {
        private readonly SchoolDbContext _context;
        private readonly ITenantContext _tenantContext;

        public UserPasswordManagementService(SchoolDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private IQueryable<User> ScopedUsersQuery()
        {
            var query = _context.Users.IgnoreQueryFilters().AsQueryable();
            if (!_tenantContext.BypassTenantFilter && _tenantContext.SchoolId.HasValue)
                query = query.Where(u => u.SchoolId == _tenantContext.SchoolId);
            return query;
        }

        public async Task<List<UserListDto>> GetAllUsersAsync()
        {
            var users = await ScopedUsersQuery()
                .OrderBy(u => u.Name)
                .ThenBy(u => u.LastName)
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    FirstName = u.Name,
                    LastName = u.LastName ?? string.Empty,
                    Email = u.Email,
                    Role = u.Role ?? string.Empty,
                    Grade = u.StudentAssignments
                        .Where(sa => sa.IsActive)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Select(sa => sa.Grade.Name)
                        .FirstOrDefault() ?? "-",
                    Group = u.StudentAssignments
                        .Where(sa => sa.IsActive)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Select(sa => sa.Group.Name)
                        .FirstOrDefault() ?? "-",
                    Status = u.Status ?? string.Empty,
                    PasswordEmailStatus = u.PasswordEmailStatus,
                    PasswordEmailSentAt = u.PasswordEmailSentAt,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
            return users;
        }

        public async Task<List<UserListDto>> GetUsersByRoleAsync(string? role)
        {
            if (string.IsNullOrWhiteSpace(role) || role.Equals("All", StringComparison.OrdinalIgnoreCase))
                return await GetAllUsersAsync();

            var roleLower = (role ?? string.Empty).Trim().ToLowerInvariant();
            var roleFilter = roleLower switch
            {
                "superadmin" => new[] { "superadmin" },
                "admin" => new[] { "admin" },
                "teacher" => new[] { "teacher" },
                "student" => new[] { "student", "estudiante" },
                _ => new[] { roleLower }
            };

            var users = await ScopedUsersQuery()
                .Where(u => roleFilter.Contains((u.Role ?? string.Empty).ToLowerInvariant()))
                .OrderBy(u => u.Name)
                .ThenBy(u => u.LastName)
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    FirstName = u.Name,
                    LastName = u.LastName ?? string.Empty,
                    Email = u.Email,
                    Role = u.Role ?? string.Empty,
                    Grade = u.StudentAssignments
                        .Where(sa => sa.IsActive)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Select(sa => sa.Grade.Name)
                        .FirstOrDefault() ?? "-",
                    Group = u.StudentAssignments
                        .Where(sa => sa.IsActive)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Select(sa => sa.Group.Name)
                        .FirstOrDefault() ?? "-",
                    Status = u.Status ?? string.Empty,
                    PasswordEmailStatus = u.PasswordEmailStatus,
                    PasswordEmailSentAt = u.PasswordEmailSentAt,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
            return users;
        }

        public async Task<UserPasswordManagementIndexViewModel> GetIndexViewModelAsync(
            Guid? gradeId,
            Guid? groupId,
            string? roleFilter,
            string? searchQuery,
            Guid? callerSchoolId,
            bool callerIsSuperAdmin,
            CancellationToken cancellationToken = default)
        {
            var vm = new UserPasswordManagementIndexViewModel
            {
                SelectedGradeId = gradeId,
                SelectedGroupId = groupId,
                SelectedRole = string.IsNullOrWhiteSpace(roleFilter) ? null : roleFilter.ToLowerInvariant(),
                SearchQuery = searchQuery
            };

            // Grados y grupos para dropdowns (alcance por escuela si no es SuperAdmin)
            var gradesQ = _context.GradeLevels.AsNoTracking().IgnoreQueryFilters();
            var groupsQ = _context.Groups.AsNoTracking().IgnoreQueryFilters();
            if (!callerIsSuperAdmin && callerSchoolId.HasValue)
            {
                gradesQ = gradesQ.Where(g => g.SchoolId == callerSchoolId.Value);
                groupsQ = groupsQ.Where(g => g.SchoolId == callerSchoolId.Value);
            }
            else if (!callerIsSuperAdmin)
            {
                vm.GradeLevels = new List<GradeLevel>();
                vm.Groups = new List<Group>();
                vm.Users = new List<UserPasswordViewModel>();
                return vm;
            }

            vm.GradeLevels = await gradesQ.OrderBy(g => g.Name).ToListAsync(cancellationToken);
            vm.Groups = await groupsQ.OrderBy(g => g.Name).ToListAsync(cancellationToken);

            var usersQ = _context.Users.IgnoreQueryFilters();
            if (!callerIsSuperAdmin && callerSchoolId.HasValue)
                usersQ = usersQ.Where(u => u.SchoolId == callerSchoolId.Value);

            // Grado y/o grupo: misma fila de asignación activa (evita combinar grado de un curso y grupo de otro).
            if (gradeId.HasValue || groupId.HasValue)
            {
                var gGrade = gradeId;
                var gGroup = groupId;
                usersQ = usersQ.Where(u =>
                    ((u.Role ?? "").ToLower() != "student" && (u.Role ?? "").ToLower() != "estudiante") ||
                    u.StudentAssignments.Any(sa =>
                        sa.IsActive &&
                        (!gGrade.HasValue || sa.GradeId == gGrade.Value) &&
                        (!gGroup.HasValue || sa.GroupId == gGroup.Value)));
            }

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                var rf = roleFilter.Trim().ToLowerInvariant();
                if (rf == "student")
                {
                    usersQ = usersQ.Where(u =>
                        (u.Role ?? "").ToLower() == "student" ||
                        (u.Role ?? "").ToLower() == "estudiante");
                }
                else
                {
                    usersQ = usersQ.Where(u => (u.Role ?? "").ToLower() == rf);
                }
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var term = searchQuery.Trim();
                var pattern = "%" + term.Replace("%", "[%]").Replace("_", "[_]") + "%";
                usersQ = usersQ.Where(u =>
                    EF.Functions.ILike(u.Name, pattern) ||
                    EF.Functions.ILike(u.LastName ?? "", pattern) ||
                    EF.Functions.ILike(u.Email, pattern));
            }

            vm.Users = await usersQ
                .OrderBy(u => u.Name)
                .ThenBy(u => u.LastName)
                .Select(u => new UserPasswordViewModel
                {
                    Id = u.Id,
                    FirstName = u.Name,
                    LastName = u.LastName ?? string.Empty,
                    Email = u.Email,
                    Role = u.Role ?? string.Empty,
                    Grade = u.StudentAssignments
                        .Where(sa => sa.IsActive)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Select(sa => sa.Grade.Name)
                        .FirstOrDefault() ?? "-",
                    Group = u.StudentAssignments
                        .Where(sa => sa.IsActive)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Select(sa => sa.Group.Name)
                        .FirstOrDefault() ?? "-",
                    Status = u.Status ?? string.Empty,
                    PasswordEmailStatus = u.PasswordEmailStatus,
                    PasswordEmailSentAt = u.PasswordEmailSentAt,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return vm;
        }
    }
}
