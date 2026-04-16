using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services
{
    public class OrientationReportService : IOrientationReportService
    {
        private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public OrientationReportService(SchoolDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<OrientationReport>> GetAllAsync() =>
            await _context.OrientationReports.ToListAsync();

        public async Task<OrientationReport?> GetByIdAsync(Guid? id)
        {
            if (!id.HasValue)
                return null;
            
            return await _context.OrientationReports.FindAsync(id.Value);
        }

        public async Task CreateAsync(OrientationReport report)
        {
            report.CreatedAt = DateTime.UtcNow;
            _context.OrientationReports.Add(report);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(OrientationReport report)
        {
            report.UpdatedAt = DateTime.UtcNow;
            _context.OrientationReports.Update(report);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var report = await _context.OrientationReports.FindAsync(id);
            if (report != null)
            {
                _context.OrientationReports.Remove(report);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<OrientationReport>> GetByStudentAsync(Guid studentId)
        {
            return await _context.OrientationReports
                .Where(r => r.StudentId == studentId)
                .ToListAsync();
        }

        public async Task<List<OrientationReport>> GetFilteredAsync(DateTime? fechaInicio, DateTime? fechaFin, Guid? gradoId, Guid? groupId = null, Guid? studentId = null)
        {
            var query = _context.OrientationReports
                .Include(r => r.Student)
                .Include(r => r.Teacher)
                .Include(r => r.Group)
                .Include(r => r.GradeLevel)
                .AsQueryable();

            // Filtros obligatorios
            if (gradoId.HasValue)
            {
                query = query.Where(r => r.GradeLevelId == gradoId);
            }

            // Filtros opcionales
            if (groupId.HasValue)
            {
                query = query.Where(r => r.GroupId == groupId);
            }

            if (studentId.HasValue)
            {
                query = query.Where(r => r.StudentId == studentId);
            }

            if (fechaInicio.HasValue)
            {
                var startDate = fechaInicio.Value.Date.ToUniversalTime();
                query = query.Where(r => r.Date >= startDate);
            }

            if (fechaFin.HasValue)
            {
                var endDate = fechaFin.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
                query = query.Where(r => r.Date <= endDate);
            }

            return await query
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<OrientationReportDto>> GetByStudentDtoAsync(Guid studentId, string trimester = null)
        {
            // Obtener school_id del usuario autenticado
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            var schoolId = currentUser?.SchoolId;

            var query = _context.OrientationReports
                .Include(r => r.Teacher)
                .Include(r => r.Subject) // ✅ Incluir Subject
                .Where(r => r.StudentId == studentId);

            // ✅ Filtrar por school_id del usuario autenticado
            if (schoolId.HasValue)
            {
                query = query.Where(r => r.SchoolId == schoolId.Value);
            }

            if (!string.IsNullOrEmpty(trimester))
            {
                var trimesterInfo = await _context.Trimesters
                    .FirstOrDefaultAsync(t => t.Name == trimester && t.SchoolId == schoolId);

                if (trimesterInfo != null)
                {
                    var startDate = trimesterInfo.StartDate.ToUniversalTime();
                    var endDate = trimesterInfo.EndDate.ToUniversalTime();
                    query = query.Where(r => r.Date >= startDate && r.Date <= endDate);
                }
            }

            var reports = await query
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reports.Select(r => new OrientationReportDto
            {
                Type = r.ReportType,
                Category = r.Category,
                Status = r.Status,
                Description = r.Description,
                Date = r.Date,
                Documents = r.Documents,
                Teacher = r.Teacher != null ? $"{r.Teacher.Name} {r.Teacher.LastName}" : null,
                SubjectId = r.SubjectId, // ✅ SubjectId (puede ser NULL)
                SubjectName = r.Subject?.Name // ✅ SubjectName (puede ser NULL)
            }).ToList();
        }

        public async Task<List<OrientationReportDto>> GetByCounselorAsync(Guid counselorId, string trimester = null)
        {
            // Obtener school_id del usuario autenticado
            var currentUser = await _currentUserService.GetCurrentUserAsync();
            var schoolId = currentUser?.SchoolId;

            // Obtener los grupos donde el usuario es consejero
            var counselorGroups = await _context.CounselorAssignments
                .Where(ca => ca.UserId == counselorId && ca.SchoolId == schoolId && ca.IsActive)
                .Select(ca => new { ca.GroupId, ca.GradeId })
                .ToListAsync();

            if (!counselorGroups.Any())
            {
                return new List<OrientationReportDto>();
            }

            // Obtener estudiantes de los grupos donde es consejero
            var groupIds = counselorGroups.Where(cg => cg.GroupId.HasValue).Select(cg => cg.GroupId.Value).ToList();
            var gradeIds = counselorGroups.Where(cg => cg.GradeId.HasValue).Select(cg => cg.GradeId.Value).ToList();

            var studentIds = await _context.StudentSubjectAssignments
                .Where(ssa => ssa.IsActive)
                .Join(_context.SubjectAssignments,
                      ssa => ssa.SubjectAssignmentId,
                      sa => sa.Id,
                      (ssa, sa) => new { ssa.StudentId, sa.GroupId, sa.GradeLevelId })
                .Where(x => groupIds.Contains(x.GroupId) || gradeIds.Contains(x.GradeLevelId))
                .Select(x => x.StudentId)
                .Distinct()
                .ToListAsync();

            if (!studentIds.Any())
            {
                return new List<OrientationReportDto>();
            }

            var query = _context.OrientationReports
                .Include(r => r.Teacher)
                .Include(r => r.Subject)
                .Include(r => r.Student)
                .Where(r => studentIds.Contains(r.StudentId.Value));

            // Filtrar por school_id del usuario autenticado
            if (schoolId.HasValue)
            {
                query = query.Where(r => r.SchoolId == schoolId.Value);
            }

            if (!string.IsNullOrEmpty(trimester))
            {
                var trimesterInfo = await _context.Trimesters
                    .FirstOrDefaultAsync(t => t.Name == trimester && t.SchoolId == schoolId);

                if (trimesterInfo != null)
                {
                    var startDate = trimesterInfo.StartDate.ToUniversalTime();
                    var endDate = trimesterInfo.EndDate.ToUniversalTime();
                    query = query.Where(r => r.Date >= startDate && r.Date <= endDate);
                }
            }

            var reports = await query
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reports.Select(r => new OrientationReportDto
            {
                Id = r.Id,
                Type = r.ReportType,
                Category = r.Category,
                Status = r.Status,
                Description = r.Description,
                Date = r.Date,
                Documents = r.Documents,
                Teacher = r.Teacher != null ? $"{r.Teacher.Name} {r.Teacher.LastName}" : null,
                SubjectId = r.SubjectId,
                SubjectName = r.Subject?.Name,
                StudentName = r.Student != null ? $"{r.Student.Name} {r.Student.LastName}" : null,
                StudentId = r.StudentId
            }).ToList();
        }
    }
}
