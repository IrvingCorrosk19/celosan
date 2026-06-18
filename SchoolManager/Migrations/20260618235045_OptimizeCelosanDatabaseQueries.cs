using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCelosanDatabaseQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_users_school_role_name",
                table: "users",
                columns: new[] { "school_id", "role", "last_name", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_subject_promotion_records_student_curriculum_outcome",
                table: "subject_promotion_records",
                columns: new[] { "student_id", "curriculum_subject_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_subject_assignments_school_subject_grade_status",
                table: "subject_assignments",
                columns: new[] { "SchoolId", "subject_id", "grade_level_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_subject_withdrawal_requests_school_status_created",
                table: "student_subject_withdrawal_requests",
                columns: new[] { "school_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_assignment_active",
                table: "student_subject_assignments",
                columns: new[] { "subject_assignment_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_school_status",
                table: "student_subject_assignments",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_student_curriculum_status",
                table: "student_subject_assignments",
                columns: new[] { "student_id", "curriculum_subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_assignment_status",
                table: "student_prematriculation_subject_selections",
                columns: new[] { "subject_assignment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_school_status",
                table: "student_prematriculation_subject_selections",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_identity_documents_school_status_expiration",
                table: "student_identity_documents",
                columns: new[] { "school_id", "status", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "ix_student_academic_credits_school_status",
                table: "student_academic_credits",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_academic_credits_student_status",
                table: "student_academic_credits",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_receipts_school_generated_at",
                table: "prematriculation_receipts",
                columns: new[] { "school_id", "generated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_school_action",
                table: "audit_logs",
                columns: new[] { "school_id", "action" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_school_timestamp",
                table: "audit_logs",
                columns: new[] { "school_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_school_role_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_subject_promotion_records_student_curriculum_outcome",
                table: "subject_promotion_records");

            migrationBuilder.DropIndex(
                name: "ix_subject_assignments_school_subject_grade_status",
                table: "subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_subject_withdrawal_requests_school_status_created",
                table: "student_subject_withdrawal_requests");

            migrationBuilder.DropIndex(
                name: "ix_student_subject_assignments_assignment_active",
                table: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_student_subject_assignments_school_status",
                table: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_student_subject_assignments_student_curriculum_status",
                table: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_spm_subject_selections_assignment_status",
                table: "student_prematriculation_subject_selections");

            migrationBuilder.DropIndex(
                name: "ix_spm_subject_selections_school_status",
                table: "student_prematriculation_subject_selections");

            migrationBuilder.DropIndex(
                name: "ix_student_identity_documents_school_status_expiration",
                table: "student_identity_documents");

            migrationBuilder.DropIndex(
                name: "ix_student_academic_credits_school_status",
                table: "student_academic_credits");

            migrationBuilder.DropIndex(
                name: "ix_student_academic_credits_student_status",
                table: "student_academic_credits");

            migrationBuilder.DropIndex(
                name: "ix_prematriculation_receipts_school_generated_at",
                table: "prematriculation_receipts");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_school_action",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_school_timestamp",
                table: "audit_logs");
        }
    }
}
