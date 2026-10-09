using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_installments",
                table: "card",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "paying_account_id",
                table: "card",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pays_from_account_directly",
                table: "card",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "card_billing_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_month = table.Column<DateOnly>(type: "date", nullable: false),
                    statement_day = table.Column<int>(type: "integer", nullable: false),
                    payment_day = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_billing_terms", x => x.id);
                    table.CheckConstraint("ck_card_billing_terms_payment_after_statement", "payment_day > statement_day");
                    table.CheckConstraint("ck_card_billing_terms_payment_day", "payment_day BETWEEN 2 AND 31");
                    table.CheckConstraint("ck_card_billing_terms_statement_day", "statement_day BETWEEN 1 AND 30");
                    table.ForeignKey(
                        name: "fk_card_billing_terms_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: true),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_payment", x => x.id);
                    table.CheckConstraint("ck_card_payment_paid", "(is_paid IS TRUE) = (paid_date IS NOT NULL)");
                    table.CheckConstraint("ck_card_payment_paid_account", "(is_paid IS TRUE) = (paid_account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_card_payment_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_card_payment_card_paid_account_id",
                        column: x => x.paid_account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    hidden_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "card_purchase",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    installment_count = table.Column<int>(type: "integer", nullable: false),
                    postpone_first = table.Column<bool>(type: "boolean", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_purchase", x => x.id);
                    table.CheckConstraint("ck_card_purchase_amount_positive", "amount_cents > 0");
                    table.CheckConstraint("ck_card_purchase_installment_count", "installment_count BETWEEN 1 AND 36");
                    table.CheckConstraint("ck_card_purchase_postpone_installments", "NOT postpone_first OR installment_count > 1");
                    table.ForeignKey(
                        name: "fk_card_purchase_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_card_purchase_purchase_category_category_id",
                        column: x => x.category_id,
                        principalTable: "purchase_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "card_purchase_reversal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    reversed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_purchase_reversal", x => x.id);
                    table.CheckConstraint("ck_card_purchase_reversal_installment_number", "installment_number >= 1");
                    table.ForeignKey(
                        name: "fk_card_purchase_reversal_card_purchase_purchase_id",
                        column: x => x.purchase_id,
                        principalTable: "card_purchase",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_card_paying_account_id",
                table: "card",
                column: "paying_account_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_card_max_installments",
                table: "card",
                sql: "max_installments IS NULL OR max_installments IN (12, 24, 36)");

            migrationBuilder.CreateIndex(
                name: "ux_card_billing_terms_card_id_from_month",
                table: "card_billing_terms",
                columns: new[] { "card_id", "from_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_card_payment_paid_account_id",
                table: "card_payment",
                column: "paid_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_card_payment_card_id_month",
                table: "card_payment",
                columns: new[] { "card_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_card_purchase_card_id_purchase_date",
                table: "card_purchase",
                columns: new[] { "card_id", "purchase_date" });

            migrationBuilder.CreateIndex(
                name: "ix_card_purchase_category_id",
                table: "card_purchase",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_card_purchase_reversal_purchase_id_installment_number",
                table: "card_purchase_reversal",
                columns: new[] { "purchase_id", "installment_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_card_card_paying_account_id",
                table: "card",
                column: "paying_account_id",
                principalTable: "card",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Category names are unique ignoring case (not expressible in the EF model).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_purchase_category_name_lower ON purchase_category (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_card_card_paying_account_id",
                table: "card");

            migrationBuilder.DropTable(
                name: "card_billing_terms");

            migrationBuilder.DropTable(
                name: "card_payment");

            migrationBuilder.DropTable(
                name: "card_purchase_reversal");

            migrationBuilder.DropTable(
                name: "card_purchase");

            migrationBuilder.DropTable(
                name: "purchase_category");

            migrationBuilder.DropIndex(
                name: "ix_card_paying_account_id",
                table: "card");

            migrationBuilder.DropCheckConstraint(
                name: "ck_card_max_installments",
                table: "card");

            migrationBuilder.DropColumn(
                name: "max_installments",
                table: "card");

            migrationBuilder.DropColumn(
                name: "paying_account_id",
                table: "card");

            migrationBuilder.DropColumn(
                name: "pays_from_account_directly",
                table: "card");
        }
    }
}
