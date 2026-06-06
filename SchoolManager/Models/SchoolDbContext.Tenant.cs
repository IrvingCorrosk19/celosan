using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Implementations;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Models;

public partial class SchoolDbContext
{
    private readonly ITenantContext _tenantContext = BypassTenantContext.Instance;

    public SchoolDbContext(DbContextOptions<SchoolDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(u =>
            _tenantContext.BypassTenantFilter ||
            (u.SchoolId != null && u.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Group>().HasQueryFilter(g =>
            _tenantContext.BypassTenantFilter ||
            (g.SchoolId != null && g.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<GradeLevel>().HasQueryFilter(g =>
            _tenantContext.BypassTenantFilter ||
            (g.SchoolId != null && g.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Subject>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<SubjectAssignment>().HasQueryFilter(sa =>
            _tenantContext.BypassTenantFilter ||
            (sa.SchoolId != null && sa.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Activity>().HasQueryFilter(a =>
            _tenantContext.BypassTenantFilter ||
            (a.SchoolId != null && a.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Attendance>().HasQueryFilter(a =>
            _tenantContext.BypassTenantFilter ||
            (a.SchoolId != null && a.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<StudentActivityScore>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<StudentSubjectAssignment>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Trimester>().HasQueryFilter(t =>
            _tenantContext.BypassTenantFilter ||
            (t.SchoolId != null && t.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<AcademicYear>().HasQueryFilter(y =>
            _tenantContext.BypassTenantFilter || y.SchoolId == _tenantContext.SchoolId);

        modelBuilder.Entity<Student>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Shift>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Specialty>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<DisciplineReport>().HasQueryFilter(d =>
            _tenantContext.BypassTenantFilter ||
            (d.SchoolId != null && d.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<OrientationReport>().HasQueryFilter(o =>
            _tenantContext.BypassTenantFilter ||
            (o.SchoolId != null && o.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<Prematriculation>().HasQueryFilter(p =>
            _tenantContext.BypassTenantFilter || p.SchoolId == _tenantContext.SchoolId);

        modelBuilder.Entity<Payment>().HasQueryFilter(p =>
            _tenantContext.BypassTenantFilter || p.SchoolId == _tenantContext.SchoolId);

        modelBuilder.Entity<SubjectPromotionRecord>().HasQueryFilter(r =>
            _tenantContext.BypassTenantFilter ||
            (r.SchoolId != null && r.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<CounselorAssignment>().HasQueryFilter(c =>
            _tenantContext.BypassTenantFilter || c.SchoolId == _tenantContext.SchoolId);

        modelBuilder.Entity<AuditLog>().HasQueryFilter(a =>
            _tenantContext.BypassTenantFilter ||
            (a.SchoolId != null && a.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<SecuritySetting>().HasQueryFilter(s =>
            _tenantContext.BypassTenantFilter ||
            (s.SchoolId != null && s.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<TeacherWorkPlan>().HasQueryFilter(p =>
            _tenantContext.BypassTenantFilter ||
            (p.SchoolId != null && p.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<TimeSlot>().HasQueryFilter(t =>
            _tenantContext.BypassTenantFilter || t.SchoolId == _tenantContext.SchoolId);

        modelBuilder.Entity<StudentAssignment>().HasQueryFilter(sa =>
            _tenantContext.BypassTenantFilter ||
            (sa.Student.SchoolId != null && sa.Student.SchoolId == _tenantContext.SchoolId));

        modelBuilder.Entity<TeacherAssignment>().HasQueryFilter(ta =>
            _tenantContext.BypassTenantFilter ||
            (ta.SubjectAssignment.SchoolId != null && ta.SubjectAssignment.SchoolId == _tenantContext.SchoolId));
    }
}
