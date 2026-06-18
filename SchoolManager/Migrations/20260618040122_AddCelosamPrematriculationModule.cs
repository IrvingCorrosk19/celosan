using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCelosamPrematriculationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "academic_year_id",
                table: "prematriculation_periods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_subjects_allowed",
                table: "prematriculation_periods",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "prematriculation_periods",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trimester_id",
                table: "prematriculation_periods",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prematriculation_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prematriculation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prematriculation_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consecutive = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    pdf_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("prematriculation_receipts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_prematriculation_receipts_prematriculation_periods_prematri~",
                        column: x => x.prematriculation_period_id,
                        principalTable: "prematriculation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prematriculation_receipts_prematriculations_prematriculatio~",
                        column: x => x.prematriculation_id,
                        principalTable: "prematriculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prematriculation_receipts_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prematriculation_receipts_users_generated_by",
                        column: x => x.generated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prematriculation_receipts_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prematriculation_reopen_authorizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prematriculation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prematriculation_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    authorized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    authorized_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("prematriculation_reopen_authorizations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_prematriculation_reopen_authorizations_prematriculation_per~",
                        column: x => x.prematriculation_period_id,
                        principalTable: "prematriculation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prematriculation_reopen_authorizations_prematriculations_pr~",
                        column: x => x.prematriculation_id,
                        principalTable: "prematriculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prematriculation_reopen_authorizations_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prematriculation_reopen_authorizations_users_authorized_by",
                        column: x => x.authorized_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prematriculation_reopen_authorizations_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_prematriculation_subject_selections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prematriculation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prematriculation_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curriculum_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    teacher_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    validation_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    validation_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_prematriculation_subject_selections_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_curriculum_subj~",
                        column: x => x.curriculum_subject_id,
                        principalTable: "curriculum_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_prematriculatio~",
                        column: x => x.prematriculation_id,
                        principalTable: "prematriculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_prematriculati~1",
                        column: x => x.prematriculation_period_id,
                        principalTable: "prematriculation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_schools_school_~",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_subject_assignm~",
                        column: x => x.subject_assignment_id,
                        principalTable: "subject_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_teacher_assignm~",
                        column: x => x.teacher_assignment_id,
                        principalTable: "teacher_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_prematriculation_subject_selections_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_subject_withdrawal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_subject_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    observation = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_observation = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_subject_withdrawal_requests_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_subject_withdrawal_requests_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_subject_withdrawal_requests_student_subject_assignm~",
                        column: x => x.student_subject_assignment_id,
                        principalTable: "student_subject_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_subject_withdrawal_requests_subject_assignments_sub~",
                        column: x => x.subject_assignment_id,
                        principalTable: "subject_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_subject_withdrawal_requests_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_subject_withdrawal_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_subject_withdrawal_requests_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_periods_academic_year_id",
                table: "prematriculation_periods",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_periods_trimester_id",
                table: "prematriculation_periods",
                column: "trimester_id");

            migrationBuilder.CreateIndex(
                name: "IX_prematriculation_receipts_generated_by",
                table: "prematriculation_receipts",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_receipts_prematriculation_id",
                table: "prematriculation_receipts",
                column: "prematriculation_id");

            migrationBuilder.CreateIndex(
                name: "IX_prematriculation_receipts_prematriculation_period_id",
                table: "prematriculation_receipts",
                column: "prematriculation_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_receipts_school_id",
                table: "prematriculation_receipts",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_prematriculation_receipts_student_id",
                table: "prematriculation_receipts",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_prematriculation_receipts_consecutive",
                table: "prematriculation_receipts",
                column: "consecutive",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_prematriculation_receipts_version",
                table: "prematriculation_receipts",
                columns: new[] { "prematriculation_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prematriculation_reopen_authorizations_authorized_by",
                table: "prematriculation_reopen_authorizations",
                column: "authorized_by");

            migrationBuilder.CreateIndex(
                name: "IX_prematriculation_reopen_authorizations_prematriculation_per~",
                table: "prematriculation_reopen_authorizations",
                column: "prematriculation_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_prematriculation_reopen_authorizations_student_id",
                table: "prematriculation_reopen_authorizations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_reopen_prematriculation_id",
                table: "prematriculation_reopen_authorizations",
                column: "prematriculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_prematriculation_reopen_school_id",
                table: "prematriculation_reopen_authorizations",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_curriculum_subject_id",
                table: "student_prematriculation_subject_selections",
                column: "curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_period_id",
                table: "student_prematriculation_subject_selections",
                column: "prematriculation_period_id");

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_prematriculation_id",
                table: "student_prematriculation_subject_selections",
                column: "prematriculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_school_id",
                table: "student_prematriculation_subject_selections",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_student_id",
                table: "student_prematriculation_subject_selections",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_spm_subject_selections_subject_assignment_id",
                table: "student_prematriculation_subject_selections",
                column: "subject_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_prematriculation_subject_selections_created_by",
                table: "student_prematriculation_subject_selections",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_prematriculation_subject_selections_group_id",
                table: "student_prematriculation_subject_selections",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_prematriculation_subject_selections_teacher_assignm~",
                table: "student_prematriculation_subject_selections",
                column: "teacher_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_prematriculation_subject_selections_updated_by",
                table: "student_prematriculation_subject_selections",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_spm_subject_selection",
                table: "student_prematriculation_subject_selections",
                columns: new[] { "prematriculation_id", "curriculum_subject_id" },
                unique: true,
                filter: "status <> 'Removed'");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_withdrawal_requests_requested_by",
                table: "student_subject_withdrawal_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_withdrawal_requests_reviewed_by",
                table: "student_subject_withdrawal_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_withdrawal_requests_student_id",
                table: "student_subject_withdrawal_requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_withdrawal_requests_subject_assignment_id",
                table: "student_subject_withdrawal_requests",
                column: "subject_assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_withdrawal_requests_school_id",
                table: "student_subject_withdrawal_requests",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_withdrawal_requests_ssa_id",
                table: "student_subject_withdrawal_requests",
                column: "student_subject_assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_withdrawal_requests_status",
                table: "student_subject_withdrawal_requests",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "prematriculation_periods_academic_year_id_fkey",
                table: "prematriculation_periods",
                column: "academic_year_id",
                principalTable: "academic_years",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "prematriculation_periods_trimester_id_fkey",
                table: "prematriculation_periods",
                column: "trimester_id",
                principalTable: "trimester",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "prematriculation_periods_academic_year_id_fkey",
                table: "prematriculation_periods");

            migrationBuilder.DropForeignKey(
                name: "prematriculation_periods_trimester_id_fkey",
                table: "prematriculation_periods");

            migrationBuilder.DropTable(
                name: "prematriculation_receipts");

            migrationBuilder.DropTable(
                name: "prematriculation_reopen_authorizations");

            migrationBuilder.DropTable(
                name: "student_prematriculation_subject_selections");

            migrationBuilder.DropTable(
                name: "student_subject_withdrawal_requests");

            migrationBuilder.DropIndex(
                name: "ix_prematriculation_periods_academic_year_id",
                table: "prematriculation_periods");

            migrationBuilder.DropIndex(
                name: "ix_prematriculation_periods_trimester_id",
                table: "prematriculation_periods");

            migrationBuilder.DropColumn(
                name: "academic_year_id",
                table: "prematriculation_periods");

            migrationBuilder.DropColumn(
                name: "max_subjects_allowed",
                table: "prematriculation_periods");

            migrationBuilder.DropColumn(
                name: "name",
                table: "prematriculation_periods");

            migrationBuilder.DropColumn(
                name: "trimester_id",
                table: "prematriculation_periods");
        }
    }
}
