using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRevolvingCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revolving_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    minimum_cents = table.Column<long>(type: "bigint", nullable: true),
                    interest_cents = table.Column<long>(type: "bigint", nullable: true),
                    is_paid = table.Column<bool>(type: "boolean", nullable: true),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_amount_cents = table.Column<long>(type: "bigint", nullable: true),
                    paid_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revolving_bill", x => x.id);
                    table.CheckConstraint("ck_revolving_bill_corrections", "minimum_cents >= 0 AND interest_cents >= 0");
                    table.CheckConstraint("ck_revolving_bill_paid", "(is_paid IS TRUE) = (paid_date IS NOT NULL)");
                    table.CheckConstraint("ck_revolving_bill_paid_account", "(is_paid IS TRUE) = (paid_account_id IS NOT NULL)");
                    table.CheckConstraint("ck_revolving_bill_paid_amount", "(is_paid IS TRUE) = (paid_amount_cents IS NOT NULL)");
                    table.CheckConstraint("ck_revolving_bill_paid_amount_positive", "paid_amount_cents > 0");
                    table.ForeignKey(
                        name: "fk_revolving_bill_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revolving_bill_card_paid_account_id",
                        column: x => x.paid_account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "revolving_debt_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    linked_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revolving_debt_entry", x => x.id);
                    table.CheckConstraint("ck_revolving_debt_entry_amount", "amount_cents >= 0");
                    table.CheckConstraint("ck_revolving_debt_entry_transfer_linked", "(kind IN ('TransferIn', 'TransferOut')) = (linked_card_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_revolving_debt_entry_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revolving_debt_entry_card_linked_card_id",
                        column: x => x.linked_card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "revolving_extra_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revolving_extra_payment", x => x.id);
                    table.CheckConstraint("ck_revolving_extra_payment_amount_positive", "amount_cents > 0");
                    table.ForeignKey(
                        name: "fk_revolving_extra_payment_card_account_id",
                        column: x => x.account_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revolving_extra_payment_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "revolving_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_month = table.Column<DateOnly>(type: "date", nullable: false),
                    min_payment_bp = table.Column<int>(type: "integer", nullable: false),
                    min_payment_cents = table.Column<long>(type: "bigint", nullable: false),
                    interest_bp = table.Column<int>(type: "integer", nullable: false),
                    min_payment_interest_bp = table.Column<int>(type: "integer", nullable: true),
                    bill_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revolving_terms", x => x.id);
                    table.CheckConstraint("ck_revolving_terms_bill_day", "bill_day BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_revolving_terms_due_day", "due_day BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_revolving_terms_interest_bp", "interest_bp >= 0");
                    table.CheckConstraint("ck_revolving_terms_min_payment_bp", "min_payment_bp BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_revolving_terms_min_payment_cents", "min_payment_cents >= 0");
                    table.CheckConstraint("ck_revolving_terms_min_payment_interest_bp", "min_payment_interest_bp IS NULL OR min_payment_interest_bp BETWEEN 0 AND min_payment_bp");
                    table.ForeignKey(
                        name: "fk_revolving_terms_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_card_revolving_debt_limit",
                table: "card",
                sql: "type <> 'Revolving' OR debt_limit_cents IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_revolving_bill_paid_account_id",
                table: "revolving_bill",
                column: "paid_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_revolving_bill_card_id_month",
                table: "revolving_bill",
                columns: new[] { "card_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_revolving_debt_entry_card_id_date",
                table: "revolving_debt_entry",
                columns: new[] { "card_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_revolving_debt_entry_linked_card_id",
                table: "revolving_debt_entry",
                column: "linked_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_revolving_extra_payment_account_id",
                table: "revolving_extra_payment",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_revolving_extra_payment_card_id_date",
                table: "revolving_extra_payment",
                columns: new[] { "card_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ux_revolving_terms_card_id_from_month",
                table: "revolving_terms",
                columns: new[] { "card_id", "from_month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revolving_bill");

            migrationBuilder.DropTable(
                name: "revolving_debt_entry");

            migrationBuilder.DropTable(
                name: "revolving_extra_payment");

            migrationBuilder.DropTable(
                name: "revolving_terms");

            migrationBuilder.DropCheckConstraint(
                name: "ck_card_revolving_debt_limit",
                table: "card");
        }
    }
}
