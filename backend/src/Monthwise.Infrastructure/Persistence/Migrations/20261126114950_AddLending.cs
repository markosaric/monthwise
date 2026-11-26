using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lending",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    lend_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_return_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lending", x => x.id);
                    table.CheckConstraint("ck_lending_amount_positive", "amount_cents > 0");
                    table.CheckConstraint("ck_lending_return_after_lend", "expected_return_date >= lend_date");
                    table.ForeignKey(
                        name: "fk_lending_card_from_account_id",
                        column: x => x.from_account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lending_payer_payer_id",
                        column: x => x.payer_id,
                        principalTable: "payer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lending_repayment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lending_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    to_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lending_repayment", x => x.id);
                    table.CheckConstraint("ck_lending_repayment_amount_positive", "amount_cents > 0");
                    table.ForeignKey(
                        name: "fk_lending_repayment_card_to_account_id",
                        column: x => x.to_account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lending_repayment_lending_lending_id",
                        column: x => x.lending_id,
                        principalTable: "lending",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lending_from_account_id_lend_date",
                table: "lending",
                columns: new[] { "from_account_id", "lend_date" });

            migrationBuilder.CreateIndex(
                name: "ix_lending_payer_id",
                table: "lending",
                column: "payer_id");

            migrationBuilder.CreateIndex(
                name: "ix_lending_repayment_lending_id",
                table: "lending_repayment",
                column: "lending_id");

            migrationBuilder.CreateIndex(
                name: "ix_lending_repayment_to_account_id_received_date",
                table: "lending_repayment",
                columns: new[] { "to_account_id", "received_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lending_repayment");

            migrationBuilder.DropTable(
                name: "lending");
        }
    }
}
