using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_backup_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    reminder_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    reminder_interval_days = table.Column<int>(type: "integer", nullable: false),
                    reminder_dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_backup_settings", x => x.id);
                    table.CheckConstraint("ck_data_backup_settings_single_row", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "data_safety_copy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    release = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_safety_copy", x => x.id);
                    table.CheckConstraint("ck_data_safety_copy_single_row", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "data_transfer_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    release = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_transfer_log", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "data_backup_settings",
                columns: new[] { "id", "reminder_dismissed_at", "reminder_enabled", "reminder_interval_days" },
                values: new object[] { 1, null, true, 30 });

            migrationBuilder.CreateIndex(
                name: "ix_data_transfer_log_occurred_at",
                table: "data_transfer_log",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_backup_settings");

            migrationBuilder.DropTable(
                name: "data_safety_copy");

            migrationBuilder.DropTable(
                name: "data_transfer_log");
        }
    }
}
