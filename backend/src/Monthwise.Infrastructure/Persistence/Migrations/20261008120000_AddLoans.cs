using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allows_residual_value = table.Column<bool>(type: "boolean", nullable: false),
                    is_predefined = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loan_subcategory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_predefined = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_subcategory", x => x.id);
                    table.ForeignKey(
                        name: "fk_loan_subcategory_loan_category_category_id",
                        column: x => x.category_id,
                        principalTable: "loan_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subcategory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_month = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_day = table.Column<int>(type: "integer", nullable: false),
                    monthly_payment_cents = table.Column<long>(type: "bigint", nullable: false),
                    payment_count = table.Column<int>(type: "integer", nullable: false),
                    residual_value_cents = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan", x => x.id);
                    table.CheckConstraint("ck_loan_monthly_payment_positive", "monthly_payment_cents > 0");
                    table.CheckConstraint("ck_loan_payment_count", "payment_count >= 1");
                    table.CheckConstraint("ck_loan_payment_day", "payment_day BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_loan_residual_value_positive", "residual_value_cents IS NULL OR residual_value_cents > 0");
                    table.CheckConstraint("ck_loan_standing_order_account", "(payment_method = 'StandingOrder') = (account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_loan_bank_lender_id",
                        column: x => x.lender_id,
                        principalTable: "bank",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loan_card_account_id",
                        column: x => x.account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loan_loan_category_category_id",
                        column: x => x.category_id,
                        principalTable: "loan_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loan_loan_subcategory_subcategory_id",
                        column: x => x.subcategory_id,
                        principalTable: "loan_subcategory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_changed = table.Column<bool>(type: "boolean", nullable: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: true),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_payment", x => x.id);
                    table.CheckConstraint("ck_loan_payment_amount_positive", "amount_cents IS NULL OR amount_cents > 0");
                    table.CheckConstraint("ck_loan_payment_paid", "(is_paid IS TRUE) = (paid_date IS NOT NULL)");
                    table.CheckConstraint("ck_loan_payment_paid_account", "(is_paid IS TRUE) = (paid_account_id IS NOT NULL)");
                    table.CheckConstraint("ck_loan_payment_paid_after_bill", "paid_date IS NULL OR paid_date >= month");
                    table.ForeignKey(
                        name: "fk_loan_payment_card_paid_account_id",
                        column: x => x.paid_account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loan_payment_loan_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_payment_change",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_month = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_payment_cents = table.Column<long>(type: "bigint", nullable: true),
                    payment_day = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_payment_change", x => x.id);
                    table.CheckConstraint("ck_loan_payment_change_monthly_payment_positive", "monthly_payment_cents IS NULL OR monthly_payment_cents > 0");
                    table.CheckConstraint("ck_loan_payment_change_not_empty", "monthly_payment_cents IS NOT NULL OR payment_day IS NOT NULL");
                    table.CheckConstraint("ck_loan_payment_change_payment_day", "payment_day IS NULL OR payment_day BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "fk_loan_payment_change_loan_loan_id",
                        column: x => x.loan_id,
                        principalTable: "loan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "loan_category",
                columns: new[] { "id", "allows_residual_value", "hidden_on", "is_predefined", "name" },
                values: new object[,]
                {
                    { new Guid("0199c2a0-0000-7000-8000-000000000301"), false, null, true, "Bank loan" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000302"), true, null, true, "Lease" }
                });

            migrationBuilder.InsertData(
                table: "loan_subcategory",
                columns: new[] { "id", "category_id", "hidden_on", "is_predefined", "name" },
                values: new object[,]
                {
                    { new Guid("0199c2a0-0000-7000-8000-000000000311"), new Guid("0199c2a0-0000-7000-8000-000000000301"), null, true, "Mortgage" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000312"), new Guid("0199c2a0-0000-7000-8000-000000000301"), null, true, "Cash loan" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000313"), new Guid("0199c2a0-0000-7000-8000-000000000301"), null, true, "Car loan" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000314"), new Guid("0199c2a0-0000-7000-8000-000000000301"), null, true, "Other loan" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000321"), new Guid("0199c2a0-0000-7000-8000-000000000302"), null, true, "Car lease" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000322"), new Guid("0199c2a0-0000-7000-8000-000000000302"), null, true, "Equipment lease" },
                    { new Guid("0199c2a0-0000-7000-8000-000000000323"), new Guid("0199c2a0-0000-7000-8000-000000000302"), null, true, "Other lease" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_loan_account_id",
                table: "loan",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_category_id",
                table: "loan",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_lender_id",
                table: "loan",
                column: "lender_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_subcategory_id",
                table: "loan",
                column: "subcategory_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_payment_due_date",
                table: "loan_payment",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_loan_payment_paid_account_id",
                table: "loan_payment",
                column: "paid_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_payment_paid_date",
                table: "loan_payment",
                column: "paid_date");

            migrationBuilder.CreateIndex(
                name: "ux_loan_payment_loan_id_month_kind",
                table: "loan_payment",
                columns: new[] { "loan_id", "month", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loan_payment_change_loan_id_from_month",
                table: "loan_payment_change",
                columns: new[] { "loan_id", "from_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loan_subcategory_category_id",
                table: "loan_subcategory",
                column: "category_id");

            // Names are unique ignoring case (not expressible in the EF model). Loan names only among
            // loans that are not closed, subcategory names within their category.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_loan_category_name_lower ON loan_category (lower(name));");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_loan_subcategory_name_lower ON loan_subcategory (category_id, lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_loan_name_lower ON loan (lower(name)) WHERE closed_on IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_payment");

            migrationBuilder.DropTable(
                name: "loan_payment_change");

            migrationBuilder.DropTable(
                name: "loan");

            migrationBuilder.DropTable(
                name: "loan_subcategory");

            migrationBuilder.DropTable(
                name: "loan_category");
        }
    }
}
