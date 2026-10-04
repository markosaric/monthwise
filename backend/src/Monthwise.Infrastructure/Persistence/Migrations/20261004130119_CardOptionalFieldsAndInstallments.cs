using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CardOptionalFieldsAndInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_card_has_number",
                table: "card");

            migrationBuilder.DropCheckConstraint(
                name: "ck_card_validity_order",
                table: "card");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "valid_from",
                table: "card",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<bool>(
                name: "allows_installments",
                table: "card",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_card_installments_debit",
                table: "card",
                sql: "NOT allows_installments OR type = 'Debit'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_card_validity_order",
                table: "card",
                sql: "valid_from IS NULL OR valid_through >= valid_from");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_card_installments_debit",
                table: "card");

            migrationBuilder.DropCheckConstraint(
                name: "ck_card_validity_order",
                table: "card");

            migrationBuilder.DropColumn(
                name: "allows_installments",
                table: "card");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "valid_from",
                table: "card",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_card_has_number",
                table: "card",
                sql: "card_number IS NOT NULL OR iban IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_card_validity_order",
                table: "card",
                sql: "valid_through >= valid_from");
        }
    }
}
