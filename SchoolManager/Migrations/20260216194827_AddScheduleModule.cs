using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "student_id",
                table: "scan_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // shifts debe existir antes de time_slots (FK shift_id). En BD legacy se creaba por script;
            // en instalación limpia la tabla puede no existir aún.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS shifts (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    school_id uuid NOT NULL,
    name character varying(50) NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    display_order integer NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp with time zone,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT shifts_pkey PRIMARY KEY (id)
);
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'shifts_school_id_fkey') THEN
        ALTER TABLE shifts ADD CONSTRAINT shifts_school_id_fkey
            FOREIGN KEY (school_id) REFERENCES schools(id) ON DELETE CASCADE;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS IX_shifts_school_id ON shifts(school_id);
CREATE INDEX IF NOT EXISTS IX_shifts_is_active ON shifts(is_active);
");

            migrationBuilder.CreateTable(
                name: "time_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("time_slots_pkey", x => x.id);
                    table.ForeignKey(
                        name: "time_slots_school_id_fkey",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "time_slots_shift_id_fkey",
                        column: x => x.shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "schedule_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    teacher_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<byte>(type: "smallint", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("schedule_entries_pkey", x => x.id);
                    table.ForeignKey(
                        name: "schedule_entries_academic_year_id_fkey",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "schedule_entries_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "schedule_entries_teacher_assignment_id_fkey",
                        column: x => x.teacher_assignment_id,
                        principalTable: "teacher_assignments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "schedule_entries_time_slot_id_fkey",
                        column: x => x.time_slot_id,
                        principalTable: "time_slots",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_academic_year_id",
                table: "schedule_entries",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_created_by",
                table: "schedule_entries",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_teacher_assignment_id",
                table: "schedule_entries",
                column: "teacher_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_time_slot_id",
                table: "schedule_entries",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_entries_unique_slot",
                table: "schedule_entries",
                columns: new[] { "teacher_assignment_id", "academic_year_id", "time_slot_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_time_slots_school_id",
                table: "time_slots",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_time_slots_shift_id",
                table: "time_slots",
                column: "shift_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_entries");

            migrationBuilder.DropTable(
                name: "time_slots");

            migrationBuilder.AlterColumn<Guid>(
                name: "student_id",
                table: "scan_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
