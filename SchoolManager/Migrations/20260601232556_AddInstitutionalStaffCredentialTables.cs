using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManager.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionalStaffCredentialTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "institutional_credential_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    is_printed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    printed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("institutional_credential_cards_pkey", x => x.id);
                    table.ForeignKey(
                        name: "institutional_credential_cards_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_institutional_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    employee_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("staff_institutional_profiles_pkey", x => x.user_id);
                    table.ForeignKey(
                        name: "staff_institutional_profiles_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_qr_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("staff_qr_tokens_pkey", x => x.id);
                    table.ForeignKey(
                        name: "staff_qr_tokens_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_credential_cards_card_number",
                table: "institutional_credential_cards",
                column: "card_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_credential_cards_user_id",
                table: "institutional_credential_cards",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_credential_cards_user_id_status",
                table: "institutional_credential_cards",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_qr_tokens_token",
                table: "staff_qr_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_qr_tokens_user_id",
                table: "staff_qr_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institutional_credential_cards");

            migrationBuilder.DropTable(
                name: "staff_institutional_profiles");

            migrationBuilder.DropTable(
                name: "staff_qr_tokens");
        }
    }
}
