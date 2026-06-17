using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddNocturnalModularAcademicFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "academic_credit_id",
                table: "subject_promotion_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "curriculum_subject_id",
                table: "subject_promotion_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trimester_id",
                table: "subject_promotion_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "curriculum_subject_id",
                table: "student_subject_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "period_enrollment_id",
                table: "student_subject_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trimester_id",
                table: "student_subject_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "validation_message",
                table: "student_subject_assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "validation_status",
                table: "student_subject_assignments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entry_type",
                table: "prematriculations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_equivalence_review",
                table: "prematriculations",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_trimester_id",
                table: "prematriculations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "curriculum_tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curriculum_tracks_pkey", x => x.id);
                    table.ForeignKey(
                        name: "curriculum_tracks_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curriculum_tracks_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curriculum_tracks_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curriculum_tracks_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_academic_period_enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trimester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entry_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Regular"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_academic_period_enrollments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_assignment_id_fkey",
                        column: x => x.student_assignment_id,
                        principalTable: "student_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_trimester_id_fkey",
                        column: x => x.trimester_id,
                        principalTable: "trimester",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_period_enrollments_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_subject_equivalencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_institution_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    certificate_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    document_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_subject_equivalencies_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_subject_equivalencies_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_subject_equivalencies_reviewed_by_fkey",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_subject_equivalencies_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_subject_equivalencies_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    curriculum_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    module_order = table.Column<int>(type: "integer", nullable: false),
                    credits = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    minimum_passing_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 3.0m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curriculum_subjects_pkey", x => x.id);
                    table.ForeignKey(
                        name: "curriculum_subjects_grade_level_id_fkey",
                        column: x => x.grade_level_id,
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curriculum_subjects_subject_id_fkey",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_subjects_track_id_fkey",
                        column: x => x.curriculum_track_id,
                        principalTable: "curriculum_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_subject_prerequisites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    curriculum_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prerequisite_curriculum_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Required"),
                    minimum_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    allow_equivalence = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curriculum_subject_prerequisites_pkey", x => x.id);
                    table.CheckConstraint("ck_curriculum_subject_prerequisites_not_self", "curriculum_subject_id <> prerequisite_curriculum_subject_id");
                    table.ForeignKey(
                        name: "curriculum_subject_prerequisites_prerequisite_id_fkey",
                        column: x => x.prerequisite_curriculum_subject_id,
                        principalTable: "curriculum_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_subject_prerequisites_subject_id_fkey",
                        column: x => x.curriculum_subject_id,
                        principalTable: "curriculum_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_academic_credits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curriculum_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trimester_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Valid"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_academic_credits_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_academic_credits_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_academic_credits_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_academic_credits_curriculum_subject_id_fkey",
                        column: x => x.curriculum_subject_id,
                        principalTable: "curriculum_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_credits_grade_level_id_fkey",
                        column: x => x.grade_level_id,
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_academic_credits_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_academic_credits_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_credits_subject_id_fkey",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_academic_credits_trimester_id_fkey",
                        column: x => x.trimester_id,
                        principalTable: "trimester",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_subject_equivalency_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    equivalency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curriculum_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_subject_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_score = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    normalized_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_subject_equivalency_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_subject_equivalency_items_curriculum_subject_id_fkey",
                        column: x => x.curriculum_subject_id,
                        principalTable: "curriculum_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "student_subject_equivalency_items_equivalency_id_fkey",
                        column: x => x.equivalency_id,
                        principalTable: "student_subject_equivalencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subject_promotion_records_academic_credit_id",
                table: "subject_promotion_records",
                column: "academic_credit_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_promotion_records_curriculum_subject_id",
                table: "subject_promotion_records",
                column: "curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_promotion_records_trimester_id",
                table: "subject_promotion_records",
                column: "trimester_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_curriculum_subject_id",
                table: "student_subject_assignments",
                column: "curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_period_enrollment_id",
                table: "student_subject_assignments",
                column: "period_enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_assignments_trimester_id",
                table: "student_subject_assignments",
                column: "trimester_id");

            migrationBuilder.CreateIndex(
                name: "ix_prematriculations_target_trimester_id",
                table: "prematriculations",
                column: "target_trimester_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_subject_prerequisites_prerequisite",
                table: "curriculum_subject_prerequisites",
                column: "prerequisite_curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_subject_prerequisites_subject",
                table: "curriculum_subject_prerequisites",
                column: "curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "uq_curriculum_subject_prerequisite_pair",
                table: "curriculum_subject_prerequisites",
                columns: new[] { "curriculum_subject_id", "prerequisite_curriculum_subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_subjects_grade_level",
                table: "curriculum_subjects",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_subjects_subject",
                table: "curriculum_subjects",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_subjects_track",
                table: "curriculum_subjects",
                column: "curriculum_track_id");

            migrationBuilder.CreateIndex(
                name: "uq_curriculum_subjects_track_subject_grade_order",
                table: "curriculum_subjects",
                columns: new[] { "curriculum_track_id", "subject_id", "grade_level_id", "module_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_tracks_academic_year_id",
                table: "curriculum_tracks",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_tracks_active_school_year",
                table: "curriculum_tracks",
                columns: new[] { "school_id", "academic_year_id" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_tracks_created_by",
                table: "curriculum_tracks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_tracks_school_id",
                table: "curriculum_tracks",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_tracks_updated_by",
                table: "curriculum_tracks",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_credits_academic_year_id",
                table: "student_academic_credits",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_credits_created_by",
                table: "student_academic_credits",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_credits_curriculum_subject_id",
                table: "student_academic_credits",
                column: "curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_credits_grade_level_id",
                table: "student_academic_credits",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_credits_school_id",
                table: "student_academic_credits",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_academic_credits_student",
                table: "student_academic_credits",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_academic_credits_subject",
                table: "student_academic_credits",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_credits_trimester_id",
                table: "student_academic_credits",
                column: "trimester_id");

            migrationBuilder.CreateIndex(
                name: "uq_student_academic_credits_valid_subject",
                table: "student_academic_credits",
                columns: new[] { "student_id", "curriculum_subject_id" },
                unique: true,
                filter: "status = 'Valid'");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_period_enrollments_academic_year_id",
                table: "student_academic_period_enrollments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_period_enrollments_created_by",
                table: "student_academic_period_enrollments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_student_academic_period_enrollments_school",
                table: "student_academic_period_enrollments",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_period_enrollments_student_assignment_id",
                table: "student_academic_period_enrollments",
                column: "student_assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_academic_period_enrollments_trimester",
                table: "student_academic_period_enrollments",
                column: "trimester_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_period_enrollments_updated_by",
                table: "student_academic_period_enrollments",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_student_academic_period_enrollments_active_period",
                table: "student_academic_period_enrollments",
                columns: new[] { "student_id", "academic_year_id", "trimester_id" },
                unique: true,
                filter: "status <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_equivalencies_created_by",
                table: "student_subject_equivalencies",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_equivalencies_reviewed_by",
                table: "student_subject_equivalencies",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_equivalencies_school",
                table: "student_subject_equivalencies",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_equivalencies_student",
                table: "student_subject_equivalencies",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_equivalency_items_curriculum_subject",
                table: "student_subject_equivalency_items",
                column: "curriculum_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_equivalency_items_equivalency",
                table: "student_subject_equivalency_items",
                column: "equivalency_id");

            migrationBuilder.AddForeignKey(
                name: "prematriculations_target_trimester_id_fkey",
                table: "prematriculations",
                column: "target_trimester_id",
                principalTable: "trimester",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "student_subject_assignments_curriculum_subject_id_fkey",
                table: "student_subject_assignments",
                column: "curriculum_subject_id",
                principalTable: "curriculum_subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "student_subject_assignments_period_enrollment_id_fkey",
                table: "student_subject_assignments",
                column: "period_enrollment_id",
                principalTable: "student_academic_period_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "student_subject_assignments_trimester_id_fkey",
                table: "student_subject_assignments",
                column: "trimester_id",
                principalTable: "trimester",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "subject_promotion_records_academic_credit_id_fkey",
                table: "subject_promotion_records",
                column: "academic_credit_id",
                principalTable: "student_academic_credits",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "subject_promotion_records_curriculum_subject_id_fkey",
                table: "subject_promotion_records",
                column: "curriculum_subject_id",
                principalTable: "curriculum_subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "subject_promotion_records_trimester_id_fkey",
                table: "subject_promotion_records",
                column: "trimester_id",
                principalTable: "trimester",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "prematriculations_target_trimester_id_fkey",
                table: "prematriculations");

            migrationBuilder.DropForeignKey(
                name: "student_subject_assignments_curriculum_subject_id_fkey",
                table: "student_subject_assignments");

            migrationBuilder.DropForeignKey(
                name: "student_subject_assignments_period_enrollment_id_fkey",
                table: "student_subject_assignments");

            migrationBuilder.DropForeignKey(
                name: "student_subject_assignments_trimester_id_fkey",
                table: "student_subject_assignments");

            migrationBuilder.DropForeignKey(
                name: "subject_promotion_records_academic_credit_id_fkey",
                table: "subject_promotion_records");

            migrationBuilder.DropForeignKey(
                name: "subject_promotion_records_curriculum_subject_id_fkey",
                table: "subject_promotion_records");

            migrationBuilder.DropForeignKey(
                name: "subject_promotion_records_trimester_id_fkey",
                table: "subject_promotion_records");

            migrationBuilder.DropTable(
                name: "curriculum_subject_prerequisites");

            migrationBuilder.DropTable(
                name: "student_academic_credits");

            migrationBuilder.DropTable(
                name: "student_academic_period_enrollments");

            migrationBuilder.DropTable(
                name: "student_subject_equivalency_items");

            migrationBuilder.DropTable(
                name: "curriculum_subjects");

            migrationBuilder.DropTable(
                name: "student_subject_equivalencies");

            migrationBuilder.DropTable(
                name: "curriculum_tracks");

            migrationBuilder.DropIndex(
                name: "ix_subject_promotion_records_academic_credit_id",
                table: "subject_promotion_records");

            migrationBuilder.DropIndex(
                name: "ix_subject_promotion_records_curriculum_subject_id",
                table: "subject_promotion_records");

            migrationBuilder.DropIndex(
                name: "ix_subject_promotion_records_trimester_id",
                table: "subject_promotion_records");

            migrationBuilder.DropIndex(
                name: "ix_student_subject_assignments_curriculum_subject_id",
                table: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_student_subject_assignments_period_enrollment_id",
                table: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_student_subject_assignments_trimester_id",
                table: "student_subject_assignments");

            migrationBuilder.DropIndex(
                name: "ix_prematriculations_target_trimester_id",
                table: "prematriculations");

            migrationBuilder.DropColumn(
                name: "academic_credit_id",
                table: "subject_promotion_records");

            migrationBuilder.DropColumn(
                name: "curriculum_subject_id",
                table: "subject_promotion_records");

            migrationBuilder.DropColumn(
                name: "trimester_id",
                table: "subject_promotion_records");

            migrationBuilder.DropColumn(
                name: "curriculum_subject_id",
                table: "student_subject_assignments");

            migrationBuilder.DropColumn(
                name: "period_enrollment_id",
                table: "student_subject_assignments");

            migrationBuilder.DropColumn(
                name: "trimester_id",
                table: "student_subject_assignments");

            migrationBuilder.DropColumn(
                name: "validation_message",
                table: "student_subject_assignments");

            migrationBuilder.DropColumn(
                name: "validation_status",
                table: "student_subject_assignments");

            migrationBuilder.DropColumn(
                name: "entry_type",
                table: "prematriculations");

            migrationBuilder.DropColumn(
                name: "requires_equivalence_review",
                table: "prematriculations");

            migrationBuilder.DropColumn(
                name: "target_trimester_id",
                table: "prematriculations");
        }
    }
}
