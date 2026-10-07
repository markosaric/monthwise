using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInputCashFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balance_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    balance_cents = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balance_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_balance_entry_card_account_id",
                        column: x => x.account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "input_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_predefined = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_input_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    removed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recurring_input",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_anchor = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_anchor = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_input", x => x.id);
                    table.CheckConstraint("ck_recurring_input_amount_positive", "amount_cents > 0");
                    table.CheckConstraint("ck_recurring_input_expected_after_issue", "expected_anchor >= issue_anchor");
                    table.ForeignKey(
                        name: "fk_recurring_input_card_account_id",
                        column: x => x.account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_input_input_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "input_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_input_payers_payer_id",
                        column: x => x.payer_id,
                        principalTable: "payer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_flow_input",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_date = table.Column<DateOnly>(type: "date", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recurring_input_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_flow_input", x => x.id);
                    table.CheckConstraint("ck_cash_flow_input_amount_positive", "amount_cents > 0");
                    table.CheckConstraint("ck_cash_flow_input_expected_after_issue", "expected_date >= issue_date");
                    table.CheckConstraint("ck_cash_flow_input_occurrence", "(recurring_input_id IS NULL) = (occurrence_date IS NULL)");
                    table.CheckConstraint("ck_cash_flow_input_received_after_issue", "received_date IS NULL OR received_date >= issue_date");
                    table.ForeignKey(
                        name: "fk_cash_flow_input_card_account_id",
                        column: x => x.account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_flow_input_input_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "input_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_flow_input_payers_payer_id",
                        column: x => x.payer_id,
                        principalTable: "payer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_flow_input_recurring_inputs_recurring_input_id",
                        column: x => x.recurring_input_id,
                        principalTable: "recurring_input",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skipped_occurrence",
                columns: table => new
                {
                    recurring_input_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skipped_occurrence", x => new { x.recurring_input_id, x.occurrence_date });
                    table.ForeignKey(
                        name: "fk_skipped_occurrence_recurring_input_recurring_input_id",
                        column: x => x.recurring_input_id,
                        principalTable: "recurring_input",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "input_category",
                columns: new[] { "id", "hidden_on", "is_predefined", "name" },
                values: new object[,]
                {
                    { new Guid("0199b0c0-0000-7000-8000-000000000201"), null, true, "Salary" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000202"), null, true, "Salary add-ons" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000203"), null, true, "Agreed contributions" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000204"), null, true, "Other income" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_balance_entry_account_id_date",
                table: "balance_entry",
                columns: new[] { "account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ux_balance_entry_one_starting",
                table: "balance_entry",
                column: "account_id",
                unique: true,
                filter: "kind = 'Starting'");

            migrationBuilder.CreateIndex(
                name: "ix_cash_flow_input_account_id",
                table: "cash_flow_input",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_flow_input_category_id",
                table: "cash_flow_input",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_flow_input_expected_date",
                table: "cash_flow_input",
                column: "expected_date");

            migrationBuilder.CreateIndex(
                name: "ix_cash_flow_input_payer_id",
                table: "cash_flow_input",
                column: "payer_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_flow_input_received_date",
                table: "cash_flow_input",
                column: "received_date");

            migrationBuilder.CreateIndex(
                name: "ix_cash_flow_input_recurring_input_id_occurrence_date",
                table: "cash_flow_input",
                columns: new[] { "recurring_input_id", "occurrence_date" },
                unique: true,
                filter: "recurring_input_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_input_account_id",
                table: "recurring_input",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_input_category_id",
                table: "recurring_input",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_input_payer_id",
                table: "recurring_input",
                column: "payer_id");

            // Names are unique ignoring case (not expressible in the EF model).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_payer_name_lower ON payer (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_input_category_name_lower ON input_category (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balance_entry");

            migrationBuilder.DropTable(
                name: "cash_flow_input");

            migrationBuilder.DropTable(
                name: "skipped_occurrence");

            migrationBuilder.DropTable(
                name: "recurring_input");

            migrationBuilder.DropTable(
                name: "input_category");

            migrationBuilder.DropTable(
                name: "payer");
        }
    }
}
