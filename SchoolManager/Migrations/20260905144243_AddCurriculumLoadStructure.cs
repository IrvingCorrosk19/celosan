using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumLoadStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "can_edit_curriculum_load",
                table: "users",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "curriculum_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("curriculum_blocks_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_load_subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curriculum_load_subjects_pkey", x => x.id);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_area_id_fkey",
                        column: x => x.area_id,
                        principalTable: "area",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_grade_level_id_fkey",
                        column: x => x.grade_level_id,
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_specialty_id_fkey",
                        column: x => x.specialty_id,
                        principalTable: "specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_subject_id_fkey",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_load_subjects_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_load_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    curriculum_load_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curriculum_block_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("curriculum_load_hours_pkey", x => x.id);
                    table.CheckConstraint("ck_curriculum_load_hours_non_negative", "hours >= 0");
                    table.ForeignKey(
                        name: "curriculum_load_hours_block_id_fkey",
                        column: x => x.curriculum_block_id,
                        principalTable: "curriculum_blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "curriculum_load_hours_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "curriculum_load_hours_subject_id_fkey",
                        column: x => x.curriculum_load_subject_id,
                        principalTable: "curriculum_load_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "curriculum_load_hours_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "uq_curriculum_blocks_code",
                table: "curriculum_blocks",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clh_block",
                table: "curriculum_load_hours",
                column: "curriculum_block_id");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_load_hours_created_by",
                table: "curriculum_load_hours",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_load_hours_updated_by",
                table: "curriculum_load_hours",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_curriculum_load_hours_subject_block",
                table: "curriculum_load_hours",
                columns: new[] { "curriculum_load_subject_id", "curriculum_block_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cls_grade",
                table: "curriculum_load_subjects",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_cls_school_specialty",
                table: "curriculum_load_subjects",
                columns: new[] { "school_id", "specialty_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cls_subject",
                table: "curriculum_load_subjects",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_load_subjects_area_id",
                table: "curriculum_load_subjects",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_load_subjects_created_by",
                table: "curriculum_load_subjects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_load_subjects_specialty_id",
                table: "curriculum_load_subjects",
                column: "specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_load_subjects_updated_by",
                table: "curriculum_load_subjects",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_curriculum_load_subjects_key",
                table: "curriculum_load_subjects",
                columns: new[] { "school_id", "specialty_id", "grade_level_id", "area_id", "subject_id" },
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO curriculum_blocks (id, code, name, sort_order, created_at)
VALUES
    (gen_random_uuid(), 'B1', 'Bloque 1', 1, CURRENT_TIMESTAMP),
    (gen_random_uuid(), 'B2', 'Bloque 2', 2, CURRENT_TIMESTAMP)
ON CONFLICT (code) DO NOTHING;
");

            // Estructura curricular desde oferta real, sin grupo y sin horas.
            // PREMEDIA = especialidades con grados 7/8/9; solo se copian esos grados.
            // MEDIA = el resto; solo grados 10/11/12. PRE-MEDIA 10/11 quedan fuera.
            migrationBuilder.Sql(@"
WITH grade_nums AS (
    SELECT gl.id,
           NULLIF(regexp_replace(gl.name, '\D', '', 'g'), '')::int AS grade_num
    FROM grade_levels gl
),
premedia_specialties AS (
    SELECT DISTINCT sa.specialty_id
    FROM subject_assignments sa
    JOIN grade_nums g ON g.id = sa.grade_level_id
    WHERE g.grade_num IN (7, 8, 9)
      AND sa.specialty_id IS NOT NULL
)
INSERT INTO curriculum_load_subjects (
    id, school_id, specialty_id, grade_level_id, area_id, subject_id,
    is_active, created_at, updated_at
)
SELECT
    gen_random_uuid(),
    src.school_id,
    src.specialty_id,
    src.grade_level_id,
    src.area_id,
    src.subject_id,
    TRUE,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM (
    SELECT DISTINCT
        sa.""SchoolId"" AS school_id,
        sa.specialty_id,
        sa.grade_level_id,
        sa.area_id,
        sa.subject_id
    FROM subject_assignments sa
    JOIN grade_nums g ON g.id = sa.grade_level_id
    LEFT JOIN premedia_specialties ps ON ps.specialty_id = sa.specialty_id
    WHERE sa.specialty_id IS NOT NULL
      AND sa.""SchoolId"" IS NOT NULL
      AND sa.area_id IS NOT NULL
      AND sa.subject_id IS NOT NULL
      AND (
          (ps.specialty_id IS NOT NULL AND g.grade_num IN (7, 8, 9))
          OR
          (ps.specialty_id IS NULL AND g.grade_num IN (10, 11, 12))
      )
) src;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curriculum_load_hours");

            migrationBuilder.DropTable(
                name: "curriculum_blocks");

            migrationBuilder.DropTable(
                name: "curriculum_load_subjects");

            migrationBuilder.DropColumn(
                name: "can_edit_curriculum_load",
                table: "users");
        }
    }
}
