using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SchoolManager.Models;

public partial class SchoolDbContext : DbContext
{
    public SchoolDbContext()
    {
    }

    public SchoolDbContext(DbContextOptions<SchoolDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Activity> Activities { get; set; }

    public virtual DbSet<ActivityAttachment> ActivityAttachments { get; set; }

    public virtual DbSet<ActivityType> ActivityTypes { get; set; }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<DisciplineReport> DisciplineReports { get; set; }

    public virtual DbSet<OrientationReport> OrientationReports { get; set; }

    public virtual DbSet<EmailConfiguration> EmailConfigurations { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<CounselorAssignment> CounselorAssignments { get; set; }

    public virtual DbSet<GradeLevel> GradeLevels { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<School> Schools { get; set; }

    public virtual DbSet<SecuritySetting> SecuritySettings { get; set; }

    public virtual DbSet<Specialty> Specialties { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<StudentActivityScore> StudentActivityScores { get; set; }

    public virtual DbSet<StudentAssignment> StudentAssignments { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<SubjectAssignment> SubjectAssignments { get; set; }

    public virtual DbSet<TeacherAssignment> TeacherAssignments { get; set; }

    public virtual DbSet<Trimester> Trimesters { get; set; }

    public virtual DbSet<AcademicYear> AcademicYears { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<PrematriculationPeriod> PrematriculationPeriods { get; set; }

    public virtual DbSet<Prematriculation> Prematriculations { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentConcept> PaymentConcepts { get; set; }

    public virtual DbSet<PrematriculationHistory> PrematriculationHistories { get; set; }

    public virtual DbSet<StudentIdCard> StudentIdCards { get; set; }

    public virtual DbSet<StudentPaymentAccess> StudentPaymentAccesses { get; set; }

    public virtual DbSet<StudentQrToken> StudentQrTokens { get; set; }

    public virtual DbSet<ScanLog> ScanLogs { get; set; }

    public virtual DbSet<EmailApiConfiguration> EmailApiConfigurations { get; set; }

    public virtual DbSet<EmailQueue> EmailQueues { get; set; }

    public virtual DbSet<EmailJob> EmailJobs { get; set; }

    public virtual DbSet<SchoolIdCardSetting> SchoolIdCardSettings { get; set; }

    public virtual DbSet<IdCardTemplateField> IdCardTemplateFields { get; set; }

    public virtual DbSet<TimeSlot> TimeSlots { get; set; }

    public virtual DbSet<ScheduleEntry> ScheduleEntries { get; set; }

    public virtual DbSet<SchoolScheduleConfiguration> SchoolScheduleConfigurations { get; set; }

    public virtual DbSet<TeacherWorkPlan> TeacherWorkPlans { get; set; }
    public virtual DbSet<TeacherWorkPlanDetail> TeacherWorkPlanDetails { get; set; }
    public virtual DbSet<TeacherWorkPlanReviewLog> TeacherWorkPlanReviewLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new DateTimeInterceptor());
        if (optionsBuilder.IsConfigured) return;
        optionsBuilder.UseNpgsql("Host=dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com;Database=schoolmanager_daqf;Username=admin;Password=iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r;Port=5432;SSL Mode=Require;Trust Server Certificate=true");
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurar DateTime globalmente para PostgreSQL
        ConfigureDateTimeHandling(modelBuilder);
        
        modelBuilder
            .HasPostgresExtension("pgcrypto")
            .HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("activities_pkey");

            entity.ToTable("activities");

            entity.HasIndex(e => e.ActivityTypeId, "IX_activities_ActivityTypeId");

            entity.HasIndex(e => e.TrimesterId, "IX_activities_TrimesterId");

            entity.HasIndex(e => e.SchoolId, "IX_activities_school_id");

            entity.HasIndex(e => e.SubjectId, "IX_activities_subject_id");

            entity.HasIndex(e => e.GroupId, "idx_activities_group");

            entity.HasIndex(e => e.TeacherId, "idx_activities_teacher");

            entity.HasIndex(e => e.Trimester, "idx_activities_trimester");

            entity.HasIndex(e => new { e.Name, e.Type, e.SubjectId, e.GroupId, e.TeacherId, e.Trimester }, "idx_activities_unique_lookup");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DueDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("due_date");
            entity.Property(e => e.GradeLevelId).HasColumnName("grade_level_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PdfUrl).HasColumnName("pdf_url");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.Trimester)
                .HasMaxLength(5)
                .HasColumnName("trimester");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasColumnName("type");

            entity.HasOne(d => d.ActivityType).WithMany(p => p.Activities).HasForeignKey(d => d.ActivityTypeId);

            entity.HasOne(d => d.Group).WithMany(p => p.Activities)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("activities_group_id_fkey");

            entity.HasOne(d => d.School).WithMany(p => p.Activities)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("activities_school_id_fkey");

            entity.HasOne(d => d.Subject).WithMany(p => p.Activities)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("activities_subject_id_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.Activities)
                .HasForeignKey(d => d.TeacherId)
                .HasConstraintName("activities_teacher_id_fkey");

            entity.HasOne(d => d.TrimesterNavigation).WithMany(p => p.Activities).HasForeignKey(d => d.TrimesterId);

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<ActivityAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("activity_attachments_pkey");

            entity.ToTable("activity_attachments");

            entity.HasIndex(e => e.ActivityId, "idx_attach_activity");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ActivityId).HasColumnName("activity_id");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.MimeType)
                .HasMaxLength(50)
                .HasColumnName("mime_type");
            entity.Property(e => e.StoragePath)
                .HasMaxLength(500)
                .HasColumnName("storage_path");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Activity).WithMany(p => p.ActivityAttachments)
                .HasForeignKey(d => d.ActivityId)
                .HasConstraintName("activity_attachments_activity_id_fkey");
        });

        modelBuilder.Entity<ActivityType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("activity_types_pkey");

            entity.ToTable("activity_types");

            entity.HasIndex(e => e.Name, "activity_types_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Color)
                .HasMaxLength(20)
                .HasColumnName("color");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0)
                .HasColumnName("display_order");
            entity.Property(e => e.Icon)
                .HasMaxLength(50)
                .HasColumnName("icon");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsGlobal)
                .HasDefaultValue(false)
                .HasColumnName("is_global");
            entity.Property(e => e.Name)
                .HasMaxLength(30)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("area_pkey");

            entity.ToTable("area");

            entity.HasIndex(e => e.Name, "area_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0)
                .HasColumnName("display_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsGlobal)
                .HasDefaultValue(false)
                .HasColumnName("is_global");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("attendance_pkey");

            entity.ToTable("attendance");

            entity.HasIndex(e => e.GradeId, "IX_attendance_grade_id");

            entity.HasIndex(e => e.GroupId, "IX_attendance_group_id");

            entity.HasIndex(e => e.StudentId, "IX_attendance_student_id");

            entity.HasIndex(e => e.TeacherId, "IX_attendance_teacher_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.GradeId).HasColumnName("grade_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");

            entity.HasOne(d => d.Grade).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.GradeId)
                .HasConstraintName("attendance_grade_id_fkey");

            entity.HasOne(d => d.Group).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("attendance_group_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.AttendanceStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("attendance_student_id_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.AttendanceTeachers)
                .HasForeignKey(d => d.TeacherId)
                .HasConstraintName("attendance_teacher_id_fkey");

            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_logs_pkey");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => e.SchoolId, "IX_audit_logs_school_id");

            entity.HasIndex(e => e.UserId, "IX_audit_logs_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(30)
                .HasColumnName("action");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .HasColumnName("ip_address");
            entity.Property(e => e.Resource)
                .HasMaxLength(50)
                .HasColumnName("resource");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("timestamp");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.UserName)
                .HasMaxLength(100)
                .HasColumnName("user_name");
            entity.Property(e => e.UserRole)
                .HasMaxLength(20)
                .HasColumnName("user_role");

            entity.HasOne(d => d.School).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("audit_logs_school_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("audit_logs_user_id_fkey");
        });

        modelBuilder.Entity<DisciplineReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("discipline_reports_pkey");

            entity.ToTable("discipline_reports");

            entity.HasIndex(e => e.GradeLevelId, "IX_discipline_reports_grade_level_id");

            entity.HasIndex(e => e.GroupId, "IX_discipline_reports_group_id");

            entity.HasIndex(e => e.StudentId, "IX_discipline_reports_student_id");

            entity.HasIndex(e => e.SubjectId, "IX_discipline_reports_subject_id");

            entity.HasIndex(e => e.TeacherId, "IX_discipline_reports_teacher_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Date)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("date");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.GradeLevelId).HasColumnName("grade_level_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.ReportType)
                .HasMaxLength(50)
                .HasColumnName("report_type");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Documents).HasColumnName("documents");
            entity.Property(e => e.DisciplineActionsJson)
                .HasColumnType("text")
                .HasColumnName("discipline_actions_json");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.DisciplineReports)
                .HasForeignKey(d => d.GradeLevelId)
                .HasConstraintName("discipline_reports_grade_level_id_fkey");

            entity.HasOne(d => d.Group).WithMany(p => p.DisciplineReports)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("discipline_reports_group_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.DisciplineReportStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("discipline_reports_student_id_fkey");

            entity.HasOne(d => d.Subject).WithMany(p => p.DisciplineReports)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("discipline_reports_subject_id_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.DisciplineReportTeachers)
                .HasForeignKey(d => d.TeacherId)
                .HasConstraintName("discipline_reports_teacher_id_fkey");

            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<OrientationReport>(entity =>
        {
            entity.ToTable("orientation_reports");

            entity.HasIndex(e => e.GradeLevelId, "IX_orientation_reports_grade_level_id");

            entity.HasIndex(e => e.GroupId, "IX_orientation_reports_group_id");

            entity.HasIndex(e => e.StudentId, "IX_orientation_reports_student_id");

            entity.HasIndex(e => e.SubjectId, "IX_orientation_reports_subject_id");

            entity.HasIndex(e => e.TeacherId, "IX_orientation_reports_teacher_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Date)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("date");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.GradeLevelId).HasColumnName("grade_level_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.ReportType)
                .HasMaxLength(50)
                .HasColumnName("report_type");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Documents).HasColumnName("documents");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.OrientationReports)
                .HasForeignKey(d => d.GradeLevelId)
                .HasConstraintName("orientation_reports_grade_level_id_fkey");

            entity.HasOne(d => d.Group).WithMany(p => p.OrientationReports)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("orientation_reports_group_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.OrientationReportStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("orientation_reports_student_id_fkey");

            entity.HasOne(d => d.Subject).WithMany(p => p.OrientationReports)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("orientation_reports_subject_id_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.OrientationReportTeachers)
                .HasForeignKey(d => d.TeacherId)
                .HasConstraintName("orientation_reports_teacher_id_fkey");

            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<GradeLevel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("grade_levels_pkey");

            entity.ToTable("grade_levels");

            entity.HasIndex(e => e.Name, "grade_levels_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("groups_pkey");

            entity.ToTable("groups");

            entity.HasIndex(e => e.SchoolId, "IX_groups_school_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Grade)
                .HasMaxLength(20)
                .HasColumnName("grade");
            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .HasColumnName("name");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");

            entity.HasOne(d => d.School).WithMany(p => p.Groups)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("groups_school_id_fkey");

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            
            entity.Property(e => e.MaxCapacity)
                .HasColumnName("max_capacity");
            
            entity.Property(e => e.Shift)
                .HasMaxLength(20)
                .HasColumnName("shift");
            
            entity.Property(e => e.ShiftId)
                .HasColumnName("shift_id");

            entity.HasOne(d => d.ShiftNavigation).WithMany(p => p.Groups)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("groups_shift_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<School>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("schools_pkey");

            entity.ToTable("schools");

            entity.HasQueryFilter(s => s.IsActive);

            entity.HasIndex(e => e.AdminId, "idx_schools_admin_id"); // Sin .IsUnique() - múltiples admins permitidos

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("address");
            entity.Property(e => e.AdminId).HasColumnName("admin_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(500)
                .HasColumnName("logo_url");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("phone");
            entity.Property(e => e.IdCardPolicy)
                .HasColumnType("text")
                .HasColumnName("id_card_policy");

            // Relación opcional con admin principal (puede ser null)
            // Múltiples admins se manejan via Users con SchoolId
            entity.HasOne(d => d.Admin).WithMany()
                .HasForeignKey(d => d.AdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SecuritySetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("security_settings_pkey");

            entity.ToTable("security_settings");

            entity.HasIndex(e => e.SchoolId, "IX_security_settings_school_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiryDays)
                .HasDefaultValue(90)
                .HasColumnName("expiry_days");
            entity.Property(e => e.MaxLoginAttempts)
                .HasDefaultValue(5)
                .HasColumnName("max_login_attempts");
            entity.Property(e => e.PasswordMinLength)
                .HasDefaultValue(8)
                .HasColumnName("password_min_length");
            entity.Property(e => e.PreventReuse)
                .HasDefaultValue(5)
                .HasColumnName("prevent_reuse");
            entity.Property(e => e.RequireLowercase)
                .HasDefaultValue(true)
                .HasColumnName("require_lowercase");
            entity.Property(e => e.RequireNumbers)
                .HasDefaultValue(true)
                .HasColumnName("require_numbers");
            entity.Property(e => e.RequireSpecial)
                .HasDefaultValue(true)
                .HasColumnName("require_special");
            entity.Property(e => e.RequireUppercase)
                .HasDefaultValue(true)
                .HasColumnName("require_uppercase");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.SessionTimeoutMinutes)
                .HasDefaultValue(30)
                .HasColumnName("session_timeout_minutes");

            entity.HasOne(d => d.School).WithMany(p => p.SecuritySettings)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("security_settings_school_id_fkey");
        });

        modelBuilder.Entity<Specialty>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("specialties_pkey");

            entity.ToTable("specialties");

            entity.HasIndex(e => e.Name, "specialties_name_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shifts_pkey");

            entity.ToTable("shifts");

            entity.HasIndex(e => e.SchoolId, "IX_shifts_school_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Description)
                .HasColumnName("description");
            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0)
                .HasColumnName("display_order");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("shifts_school_id_fkey");
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("students_pkey");

            entity.ToTable("students");

            entity.HasIndex(e => e.ParentId, "IX_students_parent_id");

            entity.HasIndex(e => e.SchoolId, "IX_students_school_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Grade)
                .HasMaxLength(20)
                .HasColumnName("grade");
            entity.Property(e => e.GroupName)
                .HasMaxLength(20)
                .HasColumnName("group_name");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");

            entity.HasOne(d => d.Parent).WithMany(p => p.Students)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("students_parent_id_fkey");

            entity.HasOne(d => d.School).WithMany(p => p.Students)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("students_school_id_fkey");
        });

        modelBuilder.Entity<StudentActivityScore>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_activity_scores_pkey");

            entity.ToTable("student_activity_scores");

            entity.HasIndex(e => e.ActivityId, "idx_scores_activity");

            entity.HasIndex(e => e.StudentId, "idx_scores_student");

            entity.HasIndex(e => new { e.StudentAssignmentId, e.ActivityId }, "uq_scores_assignment_activity").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ActivityId).HasColumnName("activity_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Score)
                .HasPrecision(2, 1)
                .HasColumnName("score");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.StudentAssignmentId).HasColumnName("student_assignment_id");

            entity.HasOne(d => d.StudentAssignment).WithMany()
                .HasForeignKey(d => d.StudentAssignmentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("student_activity_scores_student_assignment_id_fkey");

            entity.HasOne(d => d.Activity).WithMany(p => p.StudentActivityScores)
                .HasForeignKey(d => d.ActivityId)
                .HasConstraintName("student_activity_scores_activity_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.StudentActivityScores)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("student_activity_scores_student_id_fkey");

            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId);
            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);

            entity.Property(e => e.AcademicYearId).HasColumnName("academic_year_id");
            entity.HasIndex(e => e.AcademicYearId, "IX_student_activity_scores_academic_year_id");
            entity.HasIndex(e => new { e.StudentId, e.AcademicYearId }, "IX_student_activity_scores_student_academic_year");

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.StudentActivityScores)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("student_activity_scores_academic_year_id_fkey");
        });

        modelBuilder.Entity<StudentAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_assignments_pkey");

            entity.ToTable("student_assignments");

            entity.HasIndex(e => e.GradeId, "IX_student_assignments_grade_id");

            entity.HasIndex(e => e.GroupId, "IX_student_assignments_group_id");

            entity.HasIndex(e => e.StudentId, "IX_student_assignments_student_id");

            entity.HasIndex(e => e.ShiftId, "IX_student_assignments_shift_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.GradeId).HasColumnName("grade_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("end_date");

            entity.HasOne(d => d.Grade).WithMany(p => p.StudentAssignments)
                .HasForeignKey(d => d.GradeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_grade");

            entity.HasOne(d => d.Group).WithMany(p => p.StudentAssignments)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_group");

            entity.HasOne(d => d.Shift).WithMany(p => p.StudentAssignments)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("student_assignments_shift_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.StudentAssignments)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_student");

            entity.Property(e => e.AcademicYearId).HasColumnName("academic_year_id");

            entity.Property(e => e.EnrollmentType)
                .HasMaxLength(20)
                .HasDefaultValue("Regular")
                .HasColumnName("enrollment_type");
            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("start_date");

            entity.HasIndex(e => e.AcademicYearId, "IX_student_assignments_academic_year_id");
            entity.HasIndex(e => new { e.StudentId, e.IsActive }, "IX_student_assignments_student_active");
            entity.HasIndex(e => new { e.StudentId, e.AcademicYearId }, "IX_student_assignments_student_academic_year");

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.StudentAssignments)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("student_assignments_academic_year_id_fkey");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subjects_pkey");

            entity.ToTable("subjects");

            entity.HasIndex(e => e.AreaId, "IX_subjects_AreaId");

            entity.HasIndex(e => e.SchoolId, "IX_subjects_school_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.Status)
                .HasDefaultValue(true)
                .HasColumnName("status");

            entity.HasOne(d => d.Area).WithMany(p => p.Subjects).HasForeignKey(d => d.AreaId);

            entity.HasOne(d => d.School).WithMany(p => p.Subjects)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("subjects_school_id_fkey");

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<SubjectAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subject_assignments_pkey");

            entity.ToTable("subject_assignments");

            entity.HasIndex(e => e.SchoolId, "IX_subject_assignments_SchoolId");

            entity.HasIndex(e => e.AreaId, "IX_subject_assignments_area_id");

            entity.HasIndex(e => e.GradeLevelId, "IX_subject_assignments_grade_level_id");

            entity.HasIndex(e => e.GroupId, "IX_subject_assignments_group_id");

            entity.HasIndex(e => e.SubjectId, "IX_subject_assignments_subject_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.GradeLevelId).HasColumnName("grade_level_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.SpecialtyId).HasColumnName("specialty_id");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasColumnName("status");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");

            entity.HasOne(d => d.Area).WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subject_assignments_area_id_fkey");

            entity.HasOne(d => d.GradeLevel).WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.GradeLevelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subject_assignments_grade_level_id_fkey");

            entity.HasOne(d => d.Group).WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subject_assignments_group_id_fkey");

            entity.HasOne(d => d.School).WithMany(p => p.SubjectAssignments).HasForeignKey(d => d.SchoolId);

            entity.HasOne(d => d.Specialty).WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.SpecialtyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subject_assignments_specialty_id_fkey");

            entity.HasOne(d => d.Subject).WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subject_assignments_subject_id_fkey");
        });

        modelBuilder.Entity<TeacherAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("teacher_assignments_pkey");

            entity.ToTable("teacher_assignments");

            entity.HasIndex(e => e.SubjectAssignmentId, "IX_teacher_assignments_subject_assignment_id");

            entity.HasIndex(e => new { e.TeacherId, e.SubjectAssignmentId }, "teacher_assignments_teacher_id_subject_assignment_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.SubjectAssignmentId).HasColumnName("subject_assignment_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");

            entity.HasOne(d => d.SubjectAssignment).WithMany(p => p.TeacherAssignments)
                .HasForeignKey(d => d.SubjectAssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("teacher_assignments_subject_assignment_id_fkey");

            entity.HasOne(d => d.Teacher).WithMany(p => p.TeacherAssignments)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("teacher_assignments_teacher_id_fkey");
        });

        modelBuilder.Entity<Trimester>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trimester_pkey");

            entity.ToTable("trimester");

            entity.HasIndex(e => e.SchoolId, "IX_trimester_school_id");

            entity.HasIndex(e => new { e.Name, e.SchoolId }, "trimester_name_school_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("end_date");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.Order).HasColumnName("order");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("start_date");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.School).WithMany(p => p.Trimesters)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trimester_school_id_fkey");

            entity.Property(e => e.AcademicYearId).HasColumnName("academic_year_id");
            entity.HasIndex(e => e.AcademicYearId, "IX_trimester_academic_year_id");

            entity.HasOne(d => d.AcademicYear).WithMany(p => p.Trimesters)
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trimester_academic_year_id_fkey");
        });

        // Configuración de AcademicYear
        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("academic_years_pkey");

            entity.ToTable("academic_years");

            entity.HasIndex(e => e.SchoolId, "IX_academic_years_school_id");
            entity.HasIndex(e => e.IsActive, "IX_academic_years_is_active");
            entity.HasIndex(e => new { e.SchoolId, e.IsActive }, "IX_academic_years_school_active");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .IsRequired()
                .HasColumnName("school_id");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasColumnName("description");

            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("start_date");

            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("end_date");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(false)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.School).WithMany()
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("academic_years_school_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("academic_years_created_by_fkey");

            entity.HasOne(d => d.UpdatedByUser).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("academic_years_updated_by_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.SchoolId, "IX_users_school_id");

            entity.HasIndex(e => e.Role, "IX_users_role");

            entity.HasIndex(e => e.DocumentId, "users_document_id_key").IsUnique();

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DateOfBirth)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("date_of_birth");
            entity.Property(e => e.DocumentId)
                .HasMaxLength(50)
                .HasColumnName("document_id");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.LastLogin)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("last_login");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasDefaultValueSql("''::character varying")
                .HasColumnName("last_name");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(100)
                .HasColumnName("password_hash");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasColumnName("role");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TwoFactorEnabled)
                .HasDefaultValue(false)
                .HasColumnName("two_factor_enabled");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.CellphonePrimary)
                .HasMaxLength(20)
                .HasColumnName("cellphone_primary");
            entity.Property(e => e.CellphoneSecondary)
                .HasMaxLength(20)
                .HasColumnName("cellphone_secondary");
            entity.Property(e => e.Disciplina)
                .HasDefaultValue(false)
                .HasColumnName("disciplina");
            entity.Property(e => e.Inclusion)
                .HasMaxLength(10)
                .HasColumnName("inclusion");
            entity.Property(e => e.Orientacion)
                .HasDefaultValue(false)
                .HasColumnName("orientacion");
            entity.Property(e => e.Inclusivo)
                .HasDefaultValue(false)
                .HasColumnName("inclusivo");
            
            entity.Property(e => e.Shift)
                .HasMaxLength(20)
                .HasColumnName("shift");

            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("photo_url");

            entity.Property(e => e.BloodType)
                .HasMaxLength(10)
                .HasColumnName("blood_type");

            entity.Property(e => e.Allergies)
                .HasMaxLength(500)
                .HasColumnName("allergies");
            entity.Property(e => e.EmergencyContactName)
                .HasMaxLength(200)
                .HasColumnName("emergency_contact_name");
            entity.Property(e => e.EmergencyContactPhone)
                .HasMaxLength(30)
                .HasColumnName("emergency_contact_phone");
            entity.Property(e => e.EmergencyRelationship)
                .HasMaxLength(50)
                .HasColumnName("emergency_relationship");

            entity.Property(e => e.PasswordEmailStatus)
                .HasMaxLength(20)
                .HasColumnName("password_email_status");
            entity.Property(e => e.PasswordEmailSentAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("password_email_sent_at");

            entity.HasOne(d => d.SchoolNavigation).WithMany(p => p.Users)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("users_school_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany().HasForeignKey(d => d.CreatedBy);
            entity.HasOne(d => d.UpdatedByUser).WithMany().HasForeignKey(d => d.UpdatedBy);

            entity.HasMany(d => d.Grades).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserGrade",
                    r => r.HasOne<GradeLevel>().WithMany()
                        .HasForeignKey("GradeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_user_grades_grade"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_user_grades_user"),
                    j =>
                    {
                        j.HasKey("UserId", "GradeId").HasName("user_grades_pkey");
                        j.ToTable("user_grades");
                        j.HasIndex(new[] { "GradeId" }, "IX_user_grades_grade_id");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<Guid>("GradeId").HasColumnName("grade_id");
                    });

            entity.HasMany(d => d.Groups).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserGroup",
                    r => r.HasOne<Group>().WithMany()
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_user_groups_group"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_user_groups_user"),
                    j =>
                    {
                        j.HasKey("UserId", "GroupId").HasName("user_groups_pkey");
                        j.ToTable("user_groups");
                        j.HasIndex(new[] { "GroupId" }, "IX_user_groups_group_id");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<Guid>("GroupId").HasColumnName("group_id");
                    });

            entity.HasMany(d => d.Subjects).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserSubject",
                    r => r.HasOne<Subject>().WithMany()
                        .HasForeignKey("SubjectId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_user_subjects_subject"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_user_subjects_user"),
                    j =>
                    {
                        j.HasKey("UserId", "SubjectId").HasName("user_subjects_pkey");
                        j.ToTable("user_subjects");
                        j.HasIndex(new[] { "SubjectId" }, "IX_user_subjects_subject_id");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<Guid>("SubjectId").HasColumnName("subject_id");
                    });
        });

        // Configuración de EmailConfiguration
        modelBuilder.Entity<EmailConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_configurations_pkey");
            entity.ToTable("email_configurations");
            
            entity.HasIndex(e => e.SchoolId, "idx_email_configurations_school_id");
            
            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            
            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");
            
            entity.Property(e => e.SmtpServer)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("smtp_server");
            
            entity.Property(e => e.SmtpPort)
                .HasDefaultValue(587)
                .HasColumnName("smtp_port");
            
            entity.Property(e => e.SmtpUsername)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("smtp_username");
            
            entity.Property(e => e.SmtpPassword)
                .IsRequired()
                .HasColumnName("smtp_password");
            
            entity.Property(e => e.SmtpUseSsl)
                .HasDefaultValue(true)
                .HasColumnName("smtp_use_ssl");
            
            entity.Property(e => e.SmtpUseTls)
                .HasDefaultValue(true)
                .HasColumnName("smtp_use_tls");
            
            entity.Property(e => e.FromEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("from_email");
            
            entity.Property(e => e.FromName)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("from_name");
            
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            
            // Configurar la relación con School
            entity.HasOne(e => e.School)
                .WithMany()
                .HasForeignKey(e => e.SchoolId)
                .HasConstraintName("email_configurations_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CounselorAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("counselor_assignments_pkey");
            entity.ToTable("counselor_assignments");
            
            // Indexes
            entity.HasIndex(e => e.SchoolId, "idx_counselor_assignments_school");
            entity.HasIndex(e => e.UserId, "idx_counselor_assignments_user");
            entity.HasIndex(e => e.GradeId, "idx_counselor_assignments_grade");
            entity.HasIndex(e => e.GroupId, "idx_counselor_assignments_group");
            
            // Unique constraints
            entity.HasIndex(e => new { e.SchoolId, e.UserId }, "counselor_assignments_school_user_key")
                .IsUnique();
            
            // Unique constraint for specific grade-group combination
            entity.HasIndex(e => new { e.SchoolId, e.GradeId, e.GroupId }, "counselor_assignments_school_grade_group_key")
                .IsUnique()
                .HasFilter("grade_id IS NOT NULL AND group_id IS NOT NULL");
            
            // Properties
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            
            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");
            
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            
            entity.Property(e => e.GradeId)
                .HasColumnName("grade_id");
            
            entity.Property(e => e.GroupId)
                .HasColumnName("group_id");
            
            entity.Property(e => e.IsCounselor)
                .HasDefaultValue(true)
                .HasColumnName("is_counselor");
            
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            
            // Foreign Keys
            entity.HasOne(e => e.School)
                .WithMany()
                .HasForeignKey(e => e.SchoolId)
                .HasConstraintName("counselor_assignments_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("counselor_assignments_user_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.GradeLevel)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .HasConstraintName("counselor_assignments_grade_id_fkey")
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Group)
                .WithMany()
                .HasForeignKey(e => e.GroupId)
                .HasConstraintName("counselor_assignments_group_id_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    /// <summary>
    /// Configura el manejo global de DateTime para PostgreSQL
    /// </summary>
    private void ConfigureDateTimeHandling(ModelBuilder modelBuilder)
    {
        // Configurar todas las propiedades DateTime para usar UTC con timezone
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    // Configurar para usar timestamp with time zone
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasColumnType("timestamp with time zone");
                    
                    // Configurar el valor por defecto para DateTime no nullable
                    if (property.ClrType == typeof(DateTime))
                    {
                        modelBuilder.Entity(entityType.ClrType)
                            .Property(property.Name)
                            .HasDefaultValueSql("CURRENT_TIMESTAMP");
                    }
                }
            }
        }

        // Configuración para Message
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messages_pkey");
            entity.ToTable("messages");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SenderId)
                .HasColumnName("sender_id");

            entity.Property(e => e.RecipientId)
                .HasColumnName("recipient_id");

            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");

            entity.Property(e => e.Subject)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("subject");

            entity.Property(e => e.Content)
                .IsRequired()
                .HasMaxLength(5000)
                .HasColumnName("content");

            entity.Property(e => e.MessageType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("message_type");

            entity.Property(e => e.GroupId)
                .HasColumnName("group_id");

            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("sent_at");

            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");

            entity.Property(e => e.ReadAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("read_at");

            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");

            entity.Property(e => e.DeletedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("deleted_at");

            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasDefaultValue("Normal")
                .HasColumnName("priority");

            entity.Property(e => e.ParentMessageId)
                .HasColumnName("parent_message_id");

            entity.Property(e => e.Attachments)
                .HasColumnName("attachments");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            // Relaciones
            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .HasConstraintName("messages_sender_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Recipient)
                .WithMany()
                .HasForeignKey(e => e.RecipientId)
                .HasConstraintName("messages_recipient_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.School)
                .WithMany()
                .HasForeignKey(e => e.SchoolId)
                .HasConstraintName("messages_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                .WithMany()
                .HasForeignKey(e => e.GroupId)
                .HasConstraintName("messages_group_id_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ParentMessage)
                .WithMany(e => e.Replies)
                .HasForeignKey(e => e.ParentMessageId)
                .HasConstraintName("messages_parent_message_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            // Índices
            entity.HasIndex(e => e.SenderId, "idx_messages_sender");
            entity.HasIndex(e => e.RecipientId, "idx_messages_recipient");
            entity.HasIndex(e => e.SchoolId, "idx_messages_school");
            entity.HasIndex(e => e.SentAt, "idx_messages_sent_at");
            entity.HasIndex(e => new { e.RecipientId, e.IsRead }, "idx_messages_recipient_unread");
        });

        // Configuración de PrematriculationPeriod
        modelBuilder.Entity<PrematriculationPeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prematriculation_periods_pkey");
            entity.ToTable("prematriculation_periods");

            entity.HasIndex(e => e.SchoolId, "IX_prematriculation_periods_school_id");
            entity.HasIndex(e => new { e.SchoolId, e.StartDate, e.EndDate }, "IX_prematriculation_periods_dates");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");

            entity.Property(e => e.StartDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("start_date");

            entity.Property(e => e.EndDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("end_date");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.Property(e => e.MaxCapacityPerGroup)
                .HasDefaultValue(30)
                .HasColumnName("max_capacity_per_group");

            entity.Property(e => e.AutoAssignByShift)
                .HasDefaultValue(true)
                .HasColumnName("auto_assign_by_shift");

            entity.Property(e => e.RequiredAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0)
                .HasColumnName("required_amount");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("prematriculation_periods_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("prematriculation_periods_created_by_fkey");

            entity.HasOne(d => d.UpdatedByUser)
                .WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("prematriculation_periods_updated_by_fkey");
        });

        // Configuración de Prematriculation
        modelBuilder.Entity<Prematriculation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prematriculations_pkey");
            entity.ToTable("prematriculations");

            entity.HasIndex(e => e.SchoolId, "IX_prematriculations_school_id");
            entity.HasIndex(e => e.StudentId, "IX_prematriculations_student_id");
            entity.HasIndex(e => e.PrematriculationPeriodId, "IX_prematriculations_period_id");
            entity.HasIndex(e => e.PrematriculationCode, "IX_prematriculations_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");

            entity.Property(e => e.StudentId)
                .HasColumnName("student_id");

            entity.Property(e => e.ParentId)
                .HasColumnName("parent_id");

            entity.Property(e => e.GradeId)
                .HasColumnName("grade_id");

            entity.Property(e => e.GroupId)
                .HasColumnName("group_id");

            entity.Property(e => e.PrematriculationPeriodId)
                .HasColumnName("prematriculation_period_id");

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pendiente")
                .HasColumnName("status");

            entity.Property(e => e.FailedSubjectsCount)
                .HasColumnName("failed_subjects_count");

            entity.Property(e => e.AcademicConditionValid)
                .HasColumnName("academic_condition_valid");

            entity.Property(e => e.RejectionReason)
                .HasColumnName("rejection_reason");

            entity.Property(e => e.PrematriculationCode)
                .HasMaxLength(50)
                .HasColumnName("prematriculation_code");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.Property(e => e.PaymentDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("payment_date");

            entity.Property(e => e.MatriculationDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("matriculation_date");

            entity.Property(e => e.ConfirmedBy)
                .HasColumnName("confirmed_by");

            entity.Property(e => e.RejectedBy)
                .HasColumnName("rejected_by");

            entity.Property(e => e.CancelledBy)
                .HasColumnName("cancelled_by");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("prematriculations_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("prematriculations_student_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Parent)
                .WithMany()
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("prematriculations_parent_id_fkey");

            entity.HasOne(d => d.Grade)
                .WithMany()
                .HasForeignKey(d => d.GradeId)
                .HasConstraintName("prematriculations_grade_id_fkey");

            entity.HasOne(d => d.Group)
                .WithMany(p => p.Prematriculations)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("prematriculations_group_id_fkey");

            entity.HasOne(d => d.PrematriculationPeriod)
                .WithMany(p => p.Prematriculations)
                .HasForeignKey(d => d.PrematriculationPeriodId)
                .HasConstraintName("prematriculations_period_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(d => d.ConfirmedBy)
                .HasConstraintName("prematriculations_confirmed_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.RejectedByUser)
                .WithMany()
                .HasForeignKey(d => d.RejectedBy)
                .HasConstraintName("prematriculations_rejected_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.CancelledByUser)
                .WithMany()
                .HasForeignKey(d => d.CancelledBy)
                .HasConstraintName("prematriculations_cancelled_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de Payment
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payments_pkey");
            entity.ToTable("payments");

            entity.HasIndex(e => e.SchoolId, "IX_payments_school_id");
            entity.HasIndex(e => e.PrematriculationId, "IX_payments_prematriculation_id");
            entity.HasIndex(e => e.ReceiptNumber, "IX_payments_receipt_number").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");

            entity.Property(e => e.PrematriculationId)
                .HasColumnName("prematriculation_id");

            entity.Property(e => e.RegisteredBy)
                .HasColumnName("registered_by");

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");

            entity.Property(e => e.PaymentDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("payment_date");

            entity.Property(e => e.ReceiptNumber)
                .HasMaxLength(100)
                .HasColumnName("receipt_number");

            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Pendiente")
                .HasColumnName("payment_status");

            entity.Property(e => e.Notes)
                .HasColumnName("notes");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.Property(e => e.ConfirmedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("confirmed_at");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("payments_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Prematriculation)
                .WithMany(p => p.Payments)
                .HasForeignKey(d => d.PrematriculationId)
                .HasConstraintName("payments_prematriculation_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");

            entity.Property(e => e.ReceiptImage)
                .HasColumnName("receipt_image");

            entity.Property(e => e.PaymentConceptId)
                .HasColumnName("payment_concept_id");

            entity.Property(e => e.StudentId)
                .HasColumnName("student_id");

            entity.HasOne(d => d.RegisteredByUser)
                .WithMany()
                .HasForeignKey(d => d.RegisteredBy)
                .HasConstraintName("payments_registered_by_fkey");

            entity.HasOne(d => d.PaymentConcept)
                .WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaymentConceptId)
                .HasConstraintName("payments_payment_concept_id_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("payments_student_id_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de PaymentConcept
        modelBuilder.Entity<PaymentConcept>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_concepts_pkey");
            entity.ToTable("payment_concepts");

            entity.HasIndex(e => e.SchoolId, "IX_payment_concepts_school_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasColumnName("description");

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");

            entity.Property(e => e.Periodicity)
                .HasMaxLength(50)
                .HasColumnName("periodicity");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("payment_concepts_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("payment_concepts_created_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.UpdatedByUser)
                .WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("payment_concepts_updated_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de PrematriculationHistory
        modelBuilder.Entity<PrematriculationHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prematriculation_histories_pkey");
            entity.ToTable("prematriculation_histories");

            entity.HasIndex(e => e.PrematriculationId, "IX_prematriculation_histories_prematriculation_id");
            entity.HasIndex(e => e.ChangedAt, "IX_prematriculation_histories_changed_at");
            entity.HasIndex(e => e.ChangedBy, "IX_prematriculation_histories_changed_by");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.PrematriculationId)
                .HasColumnName("prematriculation_id");

            entity.Property(e => e.PreviousStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("previous_status");

            entity.Property(e => e.NewStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("new_status");

            entity.Property(e => e.ChangedBy)
                .HasColumnName("changed_by");

            entity.Property(e => e.Reason)
                .HasColumnName("reason");

            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("changed_at");

            entity.Property(e => e.AdditionalInfo)
                .HasColumnName("additional_info");

            entity.HasOne(d => d.Prematriculation)
                .WithMany()
                .HasForeignKey(d => d.PrematriculationId)
                .HasConstraintName("prematriculation_histories_prematriculation_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ChangedByUser)
                .WithMany()
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("prematriculation_histories_changed_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de StudentIdCard
        modelBuilder.Entity<StudentIdCard>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_id_cards_pkey");

            entity.ToTable("student_id_cards");

            entity.HasIndex(e => e.CardNumber, "IX_student_id_cards_card_number").IsUnique();

            entity.HasIndex(e => e.StudentId, "IX_student_id_cards_student_id");

            entity.HasIndex(e => new { e.StudentId, e.Status }, "IX_student_id_cards_student_id_status");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.StudentId)
                .HasColumnName("student_id");

            entity.Property(e => e.CardNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("card_number");

            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("issued_at");

            entity.Property(e => e.ExpiresAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("expires_at");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .HasColumnName("status");

            entity.Property(e => e.IsPrinted)
                .HasDefaultValue(false)
                .HasColumnName("is_printed");

            entity.Property(e => e.PrintedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("printed_at");

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("student_id_cards_student_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de StudentPaymentAccess (módulo Club de Padres)
        modelBuilder.Entity<StudentPaymentAccess>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_payment_access_pkey");
            entity.ToTable("student_payment_access");

            entity.HasIndex(e => e.StudentId, "IX_student_payment_access_student_id");
            entity.HasIndex(e => e.SchoolId, "IX_student_payment_access_school_id");
            entity.HasIndex(e => new { e.CarnetStatus, e.SchoolId }, "IX_student_payment_access_carnet_status_school_id");
            entity.HasIndex(e => new { e.StudentId, e.SchoolId }, "IX_student_payment_access_student_id_school_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.StudentId)
                .HasColumnName("student_id");

            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");

            entity.Property(e => e.CarnetStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pendiente")
                .HasColumnName("carnet_status");

            entity.Property(e => e.PlatformAccessStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pendiente")
                .HasColumnName("platform_access_status");

            entity.Property(e => e.CarnetStatusUpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("carnet_status_updated_at");

            entity.Property(e => e.PlatformStatusUpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("platform_status_updated_at");

            entity.Property(e => e.CarnetUpdatedByUserId)
                .HasColumnName("carnet_updated_by_user_id");

            entity.Property(e => e.PlatformUpdatedByUserId)
                .HasColumnName("platform_updated_by_user_id");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("student_payment_access_student_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("student_payment_access_school_id_fkey")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CarnetUpdatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CarnetUpdatedByUserId)
                .HasConstraintName("student_payment_access_carnet_updated_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.PlatformUpdatedByUser)
                .WithMany()
                .HasForeignKey(d => d.PlatformUpdatedByUserId)
                .HasConstraintName("student_payment_access_platform_updated_by_fkey")
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de StudentQrToken
        modelBuilder.Entity<StudentQrToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_qr_tokens_pkey");

            entity.ToTable("student_qr_tokens");

            entity.HasIndex(e => e.Token, "IX_student_qr_tokens_token").IsUnique();

            entity.HasIndex(e => e.StudentId, "IX_student_qr_tokens_student_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.StudentId)
                .HasColumnName("student_id");

            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("token");

            entity.Property(e => e.ExpiresAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("expires_at");

            entity.Property(e => e.IsRevoked)
                .HasDefaultValue(false)
                .HasColumnName("is_revoked");

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("student_qr_tokens_student_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de ScanLog
        modelBuilder.Entity<ScanLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("scan_logs_pkey");

            entity.ToTable("scan_logs");

            entity.HasIndex(e => e.StudentId, "IX_scan_logs_student_id");

            entity.HasIndex(e => e.ScannedAt, "IX_scan_logs_scanned_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.StudentId)
                .HasColumnName("student_id");

            entity.Property(e => e.ScanType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("scan_type");

            entity.Property(e => e.Result)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("result");

            entity.Property(e => e.ScannedBy)
                .HasColumnName("scanned_by");

            entity.Property(e => e.ScannedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("scanned_at");

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("scan_logs_student_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailApiConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_api_configurations_pkey");
            entity.ToTable("email_api_configurations");
            entity.HasIndex(e => e.IsActive, "IX_email_api_configurations_is_active");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Provider)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("provider");
            entity.Property(e => e.ApiKey)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("api_key");
            entity.Property(e => e.FromEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("from_email");
            entity.Property(e => e.FromName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("from_name");
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<EmailJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_jobs_pkey");
            entity.ToTable("email_jobs");
            entity.HasIndex(e => e.CorrelationId, "IX_email_jobs_correlation_id");
            entity.HasIndex(e => e.RequestedAt, "IX_email_jobs_requested_at");
            entity.HasIndex(e => e.Status, "IX_email_jobs_status");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("requested_at");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue(EmailJobStatus.Accepted)
                .HasColumnName("status");
            entity.Property(e => e.TotalItems).HasDefaultValue(0).HasColumnName("total_items");
            entity.Property(e => e.SentCount).HasDefaultValue(0).HasColumnName("sent_count");
            entity.Property(e => e.FailedCount).HasDefaultValue(0).HasColumnName("failed_count");
            entity.Property(e => e.RejectedCount).HasDefaultValue(0).HasColumnName("rejected_count");
            entity.Property(e => e.SummaryJson).HasColumnType("text").HasColumnName("summary_json");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_email_jobs_users_created_by_user_id");
        });

        modelBuilder.Entity<EmailQueue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_queues_pkey");
            entity.ToTable("email_queues");
            entity.HasIndex(e => e.Status, "IX_email_queues_status");
            entity.HasIndex(e => e.CreatedAt, "IX_email_queues_created_at");
            entity.HasIndex(e => e.JobId, "IX_email_queues_job_id");
            entity.HasIndex(e => e.LockedUntil, "IX_email_queues_locked_until");
            entity.HasIndex(e => e.NextAttemptAt, "IX_email_queues_next_attempt_at");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.Subject).HasMaxLength(500).HasColumnName("subject");
            entity.Property(e => e.Body).HasColumnType("text").HasColumnName("body");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue(EmailQueueStatus.Pending)
                .HasColumnName("status");
            entity.Property(e => e.Attempts).HasDefaultValue(0).HasColumnName("attempts");
            entity.Property(e => e.MaxAttempts).HasDefaultValue(3).HasColumnName("max_attempts");
            entity.Property(e => e.LastError).HasMaxLength(2000).HasColumnName("last_error");
            entity.Property(e => e.ErrorCode).HasMaxLength(50).HasColumnName("error_code");
            entity.Property(e => e.ProviderMessageId).HasMaxLength(200).HasColumnName("provider_message_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ProcessedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("processed_at");
            entity.Property(e => e.LockedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("locked_at");
            entity.Property(e => e.LockedUntil)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("locked_until");
            entity.Property(e => e.LockedBy).HasMaxLength(100).HasColumnName("locked_by");
            entity.Property(e => e.NextAttemptAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("next_attempt_at");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Job).WithMany(j => j.QueueItems).HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_email_queues_email_jobs_job_id");
        });

        // Configuración de SchoolIdCardSetting
        modelBuilder.Entity<SchoolIdCardSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("school_id_card_settings_pkey");

            entity.ToTable("school_id_card_settings");

            entity.HasIndex(e => e.SchoolId, "IX_school_id_card_settings_school_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .IsRequired()
                .HasColumnName("school_id");

            entity.Property(e => e.TemplateKey)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("default_v1")
                .HasColumnName("template_key");

            entity.Property(e => e.PageWidthMm)
                .HasDefaultValue(55)
                .HasColumnName("page_width_mm");

            entity.Property(e => e.PageHeightMm)
                .HasDefaultValue(85)
                .HasColumnName("page_height_mm");

            entity.Property(e => e.BleedMm)
                .HasDefaultValue(0)
                .HasColumnName("bleed_mm");

            entity.Property(e => e.BackgroundColor)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("#FFFFFF")
                .HasColumnName("background_color");

            entity.Property(e => e.PrimaryColor)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("#0D6EFD")
                .HasColumnName("primary_color");

            entity.Property(e => e.TextColor)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("#111111")
                .HasColumnName("text_color");

            entity.Property(e => e.ShowQr)
                .HasDefaultValue(true)
                .HasColumnName("show_qr");

            entity.Property(e => e.ShowPhoto)
                .HasDefaultValue(true)
                .HasColumnName("show_photo");

            entity.Property(e => e.ShowSchoolPhone)
                .HasDefaultValue(true)
                .HasColumnName("show_school_phone");
            entity.Property(e => e.ShowEmergencyContact)
                .HasDefaultValue(false)
                .HasColumnName("show_emergency_contact");
            entity.Property(e => e.ShowAllergies)
                .HasDefaultValue(false)
                .HasColumnName("show_allergies");

            entity.Property(e => e.Orientation)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Vertical")
                .HasColumnName("orientation");

            entity.Property(e => e.ShowWatermark)
                .HasDefaultValue(true)
                .HasColumnName("show_watermark");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("school_id_card_settings_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de IdCardTemplateField
        modelBuilder.Entity<IdCardTemplateField>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("id_card_template_fields_pkey");

            entity.ToTable("id_card_template_fields");

            entity.HasIndex(e => e.SchoolId, "ix_id_card_template_fields_school");

            entity.HasIndex(e => e.FieldKey, "ix_id_card_template_fields_field");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");

            entity.Property(e => e.SchoolId)
                .IsRequired()
                .HasColumnName("school_id");

            entity.Property(e => e.FieldKey)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("field_key");

            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("is_enabled");

            entity.Property(e => e.XMm)
                .HasDefaultValue(0)
                .HasColumnType("decimal(6,2)")
                .HasColumnName("x_mm");

            entity.Property(e => e.YMm)
                .HasDefaultValue(0)
                .HasColumnType("decimal(6,2)")
                .HasColumnName("y_mm");

            entity.Property(e => e.WMm)
                .HasDefaultValue(0)
                .HasColumnType("decimal(6,2)")
                .HasColumnName("w_mm");

            entity.Property(e => e.HMm)
                .HasDefaultValue(0)
                .HasColumnType("decimal(6,2)")
                .HasColumnName("h_mm");

            entity.Property(e => e.FontSize)
                .HasDefaultValue(10)
                .HasColumnType("decimal(4,2)")
                .HasColumnName("font_size");

            entity.Property(e => e.FontWeight)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Normal")
                .HasColumnName("font_weight");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .HasConstraintName("id_card_template_fields_school_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Módulo Horarios: TimeSlot
        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("time_slots_pkey");

            entity.ToTable("time_slots");

            entity.HasIndex(e => e.SchoolId, "IX_time_slots_school_id");
            entity.HasIndex(e => e.ShiftId, "IX_time_slots_shift_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.SchoolId)
                .HasColumnName("school_id");
            entity.Property(e => e.ShiftId)
                .HasColumnName("shift_id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.StartTime)
                .HasColumnType("time")
                .HasColumnName("start_time");
            entity.Property(e => e.EndTime)
                .HasColumnType("time")
                .HasColumnName("end_time");
            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0)
                .HasColumnName("display_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("time_slots_school_id_fkey");

            entity.HasOne(d => d.Shift)
                .WithMany()
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("time_slots_shift_id_fkey");

            // Filtro coherente con School: solo time_slots de escuelas activas (evita warning 10622)
            entity.HasQueryFilter(t => t.School != null && t.School.IsActive);
        });

        // Módulo Horarios: ScheduleEntry
        modelBuilder.Entity<ScheduleEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("schedule_entries_pkey");

            entity.ToTable("schedule_entries");

            entity.HasIndex(e => e.TeacherAssignmentId, "IX_schedule_entries_teacher_assignment_id");
            entity.HasIndex(e => e.TimeSlotId, "IX_schedule_entries_time_slot_id");
            entity.HasIndex(e => e.AcademicYearId, "IX_schedule_entries_academic_year_id");
            entity.HasIndex(e => e.CreatedBy, "IX_schedule_entries_created_by");
            entity.HasIndex(e => new { e.TeacherAssignmentId, e.AcademicYearId, e.TimeSlotId, e.DayOfWeek }, "IX_schedule_entries_unique_slot")
                .IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.TeacherAssignmentId)
                .HasColumnName("teacher_assignment_id");
            entity.Property(e => e.TimeSlotId)
                .HasColumnName("time_slot_id");
            entity.Property(e => e.DayOfWeek)
                .HasColumnName("day_of_week");
            entity.Property(e => e.AcademicYearId)
                .HasColumnName("academic_year_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");

            entity.HasOne(d => d.AcademicYear)
                .WithMany()
                .HasForeignKey(d => d.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("schedule_entries_academic_year_id_fkey");

            entity.HasOne(d => d.TeacherAssignment)
                .WithMany()
                .HasForeignKey(d => d.TeacherAssignmentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("schedule_entries_teacher_assignment_id_fkey");

            entity.HasOne(d => d.TimeSlot)
                .WithMany(p => p.ScheduleEntries)
                .HasForeignKey(d => d.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("schedule_entries_time_slot_id_fkey");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("schedule_entries_created_by_fkey");
        });

        // Configuración de jornada escolar (una por escuela)
        modelBuilder.Entity<SchoolScheduleConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("school_schedule_configurations_pkey");
            entity.ToTable("school_schedule_configurations");
            entity.HasIndex(e => e.SchoolId).IsUnique();
            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.MorningStartTime)
                .HasColumnType("time")
                .HasColumnName("morning_start_time");
            entity.Property(e => e.MorningBlockDurationMinutes).HasColumnName("morning_block_duration_minutes");
            entity.Property(e => e.MorningBlockCount).HasColumnName("morning_block_count");
            entity.Property(e => e.RecessDurationMinutes)
                .HasColumnName("recess_duration_minutes")
                .HasDefaultValue(30);
            entity.Property(e => e.RecessAfterMorningBlockNumber)
                .HasColumnName("recess_after_morning_block_number")
                .HasDefaultValue(4);
            entity.Property(e => e.RecessAfterAfternoonBlockNumber)
                .HasColumnName("recess_after_afternoon_block_number")
                .HasDefaultValue(2);
            entity.Property(e => e.AfternoonStartTime)
                .HasColumnType("time")
                .HasColumnName("afternoon_start_time");
            entity.Property(e => e.AfternoonBlockDurationMinutes).HasColumnName("afternoon_block_duration_minutes");
            entity.Property(e => e.AfternoonBlockCount).HasColumnName("afternoon_block_count");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");
            entity.HasOne(d => d.School)
                .WithMany()
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("school_schedule_configurations_school_id_fkey");
        });

        // Plan de trabajo trimestral (docente)
        modelBuilder.Entity<TeacherWorkPlan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("teacher_work_plans_pkey");
            entity.ToTable("teacher_work_plans");
            entity.HasIndex(e => new { e.TeacherId, e.AcademicYearId, e.Trimester, e.SubjectId, e.GroupId })
                .IsUnique()
                .HasDatabaseName("ix_teacher_work_plans_teacher_year_trim_subj_group");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.GradeLevelId).HasColumnName("grade_level_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.AcademicYearId).HasColumnName("academic_year_id");
            entity.Property(e => e.Trimester).HasColumnName("trimester");
            entity.Property(e => e.Objectives).HasColumnName("objectives");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone").HasColumnName("updated_at");
            entity.Property(e => e.SchoolId).HasColumnName("school_id");
            entity.Property(e => e.SubmittedAt).HasColumnType("timestamp with time zone").HasColumnName("submitted_at");
            entity.Property(e => e.ApprovedAt).HasColumnType("timestamp with time zone").HasColumnName("approved_at");
            entity.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
            entity.Property(e => e.RejectedAt).HasColumnType("timestamp with time zone").HasColumnName("rejected_at");
            entity.Property(e => e.RejectedByUserId).HasColumnName("rejected_by_user_id");
            entity.Property(e => e.ReviewComment).HasColumnName("review_comment");
            entity.HasOne(d => d.ApprovedByUser).WithMany().HasForeignKey(d => d.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("teacher_work_plans_approved_by_fkey");
            entity.HasOne(d => d.RejectedByUser).WithMany().HasForeignKey(d => d.RejectedByUserId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("teacher_work_plans_rejected_by_fkey");
            entity.HasOne(d => d.Teacher).WithMany().HasForeignKey(d => d.TeacherId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("teacher_work_plans_teacher_id_fkey");
            entity.HasOne(d => d.Subject).WithMany().HasForeignKey(d => d.SubjectId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("teacher_work_plans_subject_id_fkey");
            entity.HasOne(d => d.GradeLevel).WithMany().HasForeignKey(d => d.GradeLevelId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("teacher_work_plans_grade_level_id_fkey");
            entity.HasOne(d => d.Group).WithMany().HasForeignKey(d => d.GroupId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("teacher_work_plans_group_id_fkey");
            entity.HasOne(d => d.AcademicYear).WithMany().HasForeignKey(d => d.AcademicYearId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("teacher_work_plans_academic_year_id_fkey");
            entity.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("teacher_work_plans_school_id_fkey");
        });

        modelBuilder.Entity<TeacherWorkPlanDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("teacher_work_plan_details_pkey");
            entity.ToTable("teacher_work_plan_details");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.TeacherWorkPlanId).HasColumnName("teacher_work_plan_id");
            entity.Property(e => e.WeeksRange).HasMaxLength(20).HasColumnName("weeks_range");
            entity.Property(e => e.Topic).HasColumnName("topic");
            entity.Property(e => e.ConceptualContent).HasColumnName("conceptual_content");
            entity.Property(e => e.ProceduralContent).HasColumnName("procedural_content");
            entity.Property(e => e.AttitudinalContent).HasColumnName("attitudinal_content");
            entity.Property(e => e.BasicCompetencies).HasColumnName("basic_competencies");
            entity.Property(e => e.AchievementIndicators).HasColumnName("achievement_indicators");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.HasOne(d => d.TeacherWorkPlan).WithMany(p => p.Details).HasForeignKey(d => d.TeacherWorkPlanId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("teacher_work_plan_details_plan_id_fkey");
        });

        modelBuilder.Entity<TeacherWorkPlanReviewLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("teacher_work_plan_review_logs_pkey");
            entity.ToTable("teacher_work_plan_review_logs");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.TeacherWorkPlanId).HasColumnName("teacher_work_plan_id");
            entity.Property(e => e.Action).HasMaxLength(50).HasColumnName("action");
            entity.Property(e => e.PerformedByUserId).HasColumnName("performed_by_user_id");
            entity.Property(e => e.PerformedAt).HasColumnType("timestamp with time zone").HasColumnName("performed_at");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.Summary).HasMaxLength(500).HasColumnName("summary");
            entity.HasOne(d => d.TeacherWorkPlan).WithMany().HasForeignKey(d => d.TeacherWorkPlanId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("teacher_work_plan_review_logs_plan_id_fkey");
            entity.HasOne(d => d.PerformedByUser).WithMany().HasForeignKey(d => d.PerformedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("teacher_work_plan_review_logs_user_fkey");
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
