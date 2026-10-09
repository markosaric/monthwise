using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillsAndExpenses : Migration
    {
        /// <summary>The starting expense categories (bills and expenses FR-1), as they were when this migration was written.</summary>
        private static readonly (string Id, string Name)[] StartingCategories =
        [
            ("0199c8b0-0000-7000-8000-000000000401", "Electricity"),
            ("0199c8b0-0000-7000-8000-000000000402", "Gas"),
            ("0199c8b0-0000-7000-8000-000000000403", "Water and waste"),
            ("0199c8b0-0000-7000-8000-000000000404", "Building reserve"),
            ("0199c8b0-0000-7000-8000-000000000405", "Internet"),
            ("0199c8b0-0000-7000-8000-000000000406", "Television"),
            ("0199c8b0-0000-7000-8000-000000000407", "Telephone"),
            ("0199c8b0-0000-7000-8000-000000000408", "Mobile phone"),
            ("0199c8b0-0000-7000-8000-000000000409", "Gym membership"),
            ("0199c8b0-0000-7000-8000-000000000410", "Other bills"),
            ("0199c8b0-0000-7000-8000-000000000421", "Groceries"),
            ("0199c8b0-0000-7000-8000-000000000422", "Personal care"),
            ("0199c8b0-0000-7000-8000-000000000423", "Kindergarten"),
            ("0199c8b0-0000-7000-8000-000000000424", "Household"),
            ("0199c8b0-0000-7000-8000-000000000425", "Transport"),
            ("0199c8b0-0000-7000-8000-000000000426", "Other expenses"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Purchase categories become the shared expense categories (installments Appendix 1): the
            // table is renamed, so the categories the user already added stay with their ids.
            migrationBuilder.DropForeignKey(
                name: "fk_card_purchase_purchase_category_category_id",
                table: "card_purchase");

            migrationBuilder.RenameTable(name: "purchase_category", newName: "expense_category");
            migrationBuilder.Sql("ALTER TABLE expense_category RENAME CONSTRAINT pk_purchase_category TO pk_expense_category;");
            migrationBuilder.Sql("ALTER INDEX ux_purchase_category_name_lower RENAME TO ux_expense_category_name_lower;");

            migrationBuilder.AddColumn<bool>(
                name: "is_predefined",
                table: "expense_category",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_fixed",
                table: "expense_category",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Starting categories (bills and expenses FR-1). A category the user already has with the same
            // name (ignoring case) is kept and marked predefined instead of being added twice.
            foreach (var (id, name) in StartingCategories)
            {
                migrationBuilder.Sql(
                    $"INSERT INTO expense_category (id, name, is_predefined, is_fixed) SELECT '{id}', '{name}', TRUE, FALSE "
                    + $"WHERE NOT EXISTS (SELECT 1 FROM expense_category WHERE lower(name) = lower('{name}'));");
                migrationBuilder.Sql($"UPDATE expense_category SET is_predefined = TRUE WHERE lower(name) = lower('{name}');");
            }

            // The fixed Cash withdrawal category (FR-8). A user's own category with that name is renamed
            // first, so the fixed one can take it.
            migrationBuilder.Sql(
                "UPDATE expense_category SET name = 'Cash withdrawal (own)' WHERE lower(name) = 'cash withdrawal';");
            migrationBuilder.Sql(
                "INSERT INTO expense_category (id, name, is_predefined, is_fixed) "
                + "VALUES ('0199c8b0-0000-7000-8000-000000000499', 'Cash withdrawal', TRUE, TRUE);");

            migrationBuilder.CreateTable(
                name: "expense",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_from_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense", x => x.id);
                    table.CheckConstraint("ck_expense_amount_positive", "amount_cents > 0");
                    table.ForeignKey(
                        name: "fk_expense_card_paid_from_id",
                        column: x => x.paid_from_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expense_expense_category_category_id",
                        column: x => x.category_id,
                        principalTable: "expense_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stopped_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_bill", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_bill_card_paid_from_id",
                        column: x => x.paid_from_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurring_bill_expense_category_category_id",
                        column: x => x.category_id,
                        principalTable: "expense_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurring_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bill", x => x.id);
                    table.CheckConstraint("ck_bill_amount", "(issue_date IS NULL AND amount_cents = 0) OR (issue_date IS NOT NULL AND amount_cents > 0)");
                    table.CheckConstraint("ck_bill_dates", "(issue_date IS NULL) = (due_date IS NULL)");
                    table.CheckConstraint("ck_bill_due_after_issue", "due_date >= issue_date");
                    table.CheckConstraint("ck_bill_paid", "is_paid = (paid_date IS NOT NULL)");
                    table.CheckConstraint("ck_bill_paid_after_issue", "paid_date >= issue_date");
                    table.CheckConstraint("ck_bill_paid_from", "NOT is_paid OR paid_from_id IS NOT NULL");
                    table.CheckConstraint("ck_bill_paid_issued", "NOT is_paid OR issue_date IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_bill_card_paid_from_id",
                        column: x => x.paid_from_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_expense_category_category_id",
                        column: x => x.category_id,
                        principalTable: "expense_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_recurring_bill_recurring_bill_id",
                        column: x => x.recurring_bill_id,
                        principalTable: "recurring_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bill_category_id",
                table: "bill",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_month",
                table: "bill",
                column: "month");

            migrationBuilder.CreateIndex(
                name: "ix_bill_paid_from_id_paid_date",
                table: "bill",
                columns: new[] { "paid_from_id", "paid_date" });

            migrationBuilder.CreateIndex(
                name: "ux_bill_recurring_bill_id_month",
                table: "bill",
                columns: new[] { "recurring_bill_id", "month" },
                unique: true,
                filter: "recurring_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_expense_category_id",
                table: "expense",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_date",
                table: "expense",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_expense_paid_from_id_date",
                table: "expense",
                columns: new[] { "paid_from_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_bill_category_id",
                table: "recurring_bill",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_bill_paid_from_id",
                table: "recurring_bill",
                column: "paid_from_id");

            // Recurring bill names are unique among the active ones, ignoring case (FR-2).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_recurring_bill_name_lower ON recurring_bill (lower(name)) WHERE stopped_on IS NULL;");

            migrationBuilder.AddForeignKey(
                name: "fk_card_purchase_expense_category_category_id",
                table: "card_purchase",
                column: "category_id",
                principalTable: "expense_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_card_purchase_expense_category_category_id",
                table: "card_purchase");

            migrationBuilder.DropTable(
                name: "bill");

            migrationBuilder.DropTable(
                name: "expense");

            migrationBuilder.DropTable(
                name: "recurring_bill");

            // Back to purchase categories: the starting and fixed categories go, the user's own stay.
            foreach (var (id, _) in StartingCategories)
            {
                migrationBuilder.Sql(
                    $"DELETE FROM expense_category WHERE id = '{id}' AND NOT EXISTS (SELECT 1 FROM card_purchase WHERE category_id = '{id}');");
            }
            migrationBuilder.Sql("DELETE FROM expense_category WHERE id = '0199c8b0-0000-7000-8000-000000000499';");

            migrationBuilder.DropColumn(name: "is_predefined", table: "expense_category");
            migrationBuilder.DropColumn(name: "is_fixed", table: "expense_category");
            migrationBuilder.Sql("ALTER INDEX ux_expense_category_name_lower RENAME TO ux_purchase_category_name_lower;");
            migrationBuilder.Sql("ALTER TABLE expense_category RENAME CONSTRAINT pk_expense_category TO pk_purchase_category;");
            migrationBuilder.RenameTable(name: "expense_category", newName: "purchase_category");

            migrationBuilder.AddForeignKey(
                name: "fk_card_purchase_purchase_category_category_id",
                table: "card_purchase",
                column: "category_id",
                principalTable: "purchase_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
