using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "contribution_account_id",
                table: "recurring_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "participant_share_count",
                table: "recurring_bill",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "share_count",
                table: "recurring_bill",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "share_participant_id",
                table: "recurring_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contribution_account_id",
                table: "loan_payment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_own_share",
                table: "loan_payment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_share_changed",
                table: "loan_payment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "participant_share_count",
                table: "loan_payment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "share_count",
                table: "loan_payment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "share_participant_id",
                table: "loan_payment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contribution_account_id",
                table: "loan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "participant_share_count",
                table: "loan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "share_count",
                table: "loan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "share_participant_id",
                table: "loan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contribution_bill_id",
                table: "cash_flow_input",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contribution_loan_id",
                table: "cash_flow_input",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "contribution_month",
                table: "cash_flow_input",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contribution_payment_kind",
                table: "cash_flow_input",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_edited",
                table: "cash_flow_input",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "contribution_account_id",
                table: "bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_share_changed",
                table: "bill",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "participant_share_count",
                table: "bill",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "share_count",
                table: "bill",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "share_participant_id",
                table: "bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "participant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    archived_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participant", x => x.id);
                    table.ForeignKey(
                        name: "fk_participant_payer_payer_id",
                        column: x => x.payer_id,
                        principalTable: "payer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_bill_contribution_account_id",
                table: "recurring_bill",
                column: "contribution_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_bill_share_participant_id",
                table: "recurring_bill",
                column: "share_participant_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurring_bill_share",
                table: "recurring_bill",
                sql: "(share_participant_id IS NULL AND share_count IS NULL AND participant_share_count IS NULL AND contribution_account_id IS NULL) OR (share_participant_id IS NOT NULL AND share_count BETWEEN 2 AND 4 AND participant_share_count >= 1 AND participant_share_count < share_count)");

            migrationBuilder.CreateIndex(
                name: "ix_loan_payment_contribution_account_id",
                table: "loan_payment",
                column: "contribution_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_payment_share_participant_id",
                table: "loan_payment",
                column: "share_participant_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_loan_payment_share",
                table: "loan_payment",
                sql: "(share_participant_id IS NULL AND share_count IS NULL AND participant_share_count IS NULL AND contribution_account_id IS NULL) OR (share_participant_id IS NOT NULL AND share_count BETWEEN 2 AND 4 AND participant_share_count >= 1 AND participant_share_count < share_count AND has_own_share)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_loan_payment_share_changed",
                table: "loan_payment",
                sql: "NOT is_share_changed OR has_own_share");

            migrationBuilder.CreateIndex(
                name: "ix_loan_contribution_account_id",
                table: "loan",
                column: "contribution_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_share_participant_id",
                table: "loan",
                column: "share_participant_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_loan_share",
                table: "loan",
                sql: "(share_participant_id IS NULL AND share_count IS NULL AND participant_share_count IS NULL AND contribution_account_id IS NULL) OR (share_participant_id IS NOT NULL AND share_count BETWEEN 2 AND 4 AND participant_share_count >= 1 AND participant_share_count < share_count)");

            migrationBuilder.CreateIndex(
                name: "ux_cash_flow_input_contribution_bill_id",
                table: "cash_flow_input",
                column: "contribution_bill_id",
                unique: true,
                filter: "contribution_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cash_flow_input_contribution_loan_payment",
                table: "cash_flow_input",
                columns: new[] { "contribution_loan_id", "contribution_month", "contribution_payment_kind" },
                unique: true,
                filter: "contribution_loan_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cash_flow_input_contribution_loan",
                table: "cash_flow_input",
                sql: "(contribution_loan_id IS NULL) = (contribution_month IS NULL) AND (contribution_loan_id IS NULL) = (contribution_payment_kind IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cash_flow_input_contribution_one_source",
                table: "cash_flow_input",
                sql: "contribution_bill_id IS NULL OR contribution_loan_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bill_contribution_account_id",
                table: "bill",
                column: "contribution_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_share_participant_id",
                table: "bill",
                column: "share_participant_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_share",
                table: "bill",
                sql: "(share_participant_id IS NULL AND share_count IS NULL AND participant_share_count IS NULL AND contribution_account_id IS NULL) OR (share_participant_id IS NOT NULL AND share_count BETWEEN 2 AND 4 AND participant_share_count >= 1 AND participant_share_count < share_count)");

            migrationBuilder.CreateIndex(
                name: "ux_participant_payer_id",
                table: "participant",
                column: "payer_id",
                unique: true);

            // Participant names are unique ignoring case (shared bills FR-1).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_participant_name_lower ON participant (lower(name));");

            migrationBuilder.AddForeignKey(
                name: "fk_bill_card_contribution_account_id",
                table: "bill",
                column: "contribution_account_id",
                principalTable: "card",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_bill_participant_share_participant_id",
                table: "bill",
                column: "share_participant_id",
                principalTable: "participant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_cash_flow_input_bill_contribution_bill_id",
                table: "cash_flow_input",
                column: "contribution_bill_id",
                principalTable: "bill",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_cash_flow_input_loan_contribution_loan_id",
                table: "cash_flow_input",
                column: "contribution_loan_id",
                principalTable: "loan",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_loan_card_contribution_account_id",
                table: "loan",
                column: "contribution_account_id",
                principalTable: "card",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_loan_participant_share_participant_id",
                table: "loan",
                column: "share_participant_id",
                principalTable: "participant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_loan_payment_card_contribution_account_id",
                table: "loan_payment",
                column: "contribution_account_id",
                principalTable: "card",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_loan_payment_participant_share_participant_id",
                table: "loan_payment",
                column: "share_participant_id",
                principalTable: "participant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_recurring_bill_card_contribution_account_id",
                table: "recurring_bill",
                column: "contribution_account_id",
                principalTable: "card",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_recurring_bill_participant_share_participant_id",
                table: "recurring_bill",
                column: "share_participant_id",
                principalTable: "participant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bill_card_contribution_account_id",
                table: "bill");

            migrationBuilder.DropForeignKey(
                name: "fk_bill_participant_share_participant_id",
                table: "bill");

            migrationBuilder.DropForeignKey(
                name: "fk_cash_flow_input_bill_contribution_bill_id",
                table: "cash_flow_input");

            migrationBuilder.DropForeignKey(
                name: "fk_cash_flow_input_loan_contribution_loan_id",
                table: "cash_flow_input");

            migrationBuilder.DropForeignKey(
                name: "fk_loan_card_contribution_account_id",
                table: "loan");

            migrationBuilder.DropForeignKey(
                name: "fk_loan_participant_share_participant_id",
                table: "loan");

            migrationBuilder.DropForeignKey(
                name: "fk_loan_payment_card_contribution_account_id",
                table: "loan_payment");

            migrationBuilder.DropForeignKey(
                name: "fk_loan_payment_participant_share_participant_id",
                table: "loan_payment");

            migrationBuilder.DropForeignKey(
                name: "fk_recurring_bill_card_contribution_account_id",
                table: "recurring_bill");

            migrationBuilder.DropForeignKey(
                name: "fk_recurring_bill_participant_share_participant_id",
                table: "recurring_bill");

            migrationBuilder.DropTable(
                name: "participant");

            migrationBuilder.DropIndex(
                name: "ix_recurring_bill_contribution_account_id",
                table: "recurring_bill");

            migrationBuilder.DropIndex(
                name: "ix_recurring_bill_share_participant_id",
                table: "recurring_bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recurring_bill_share",
                table: "recurring_bill");

            migrationBuilder.DropIndex(
                name: "ix_loan_payment_contribution_account_id",
                table: "loan_payment");

            migrationBuilder.DropIndex(
                name: "ix_loan_payment_share_participant_id",
                table: "loan_payment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_loan_payment_share",
                table: "loan_payment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_loan_payment_share_changed",
                table: "loan_payment");

            migrationBuilder.DropIndex(
                name: "ix_loan_contribution_account_id",
                table: "loan");

            migrationBuilder.DropIndex(
                name: "ix_loan_share_participant_id",
                table: "loan");

            migrationBuilder.DropCheckConstraint(
                name: "ck_loan_share",
                table: "loan");

            migrationBuilder.DropIndex(
                name: "ux_cash_flow_input_contribution_bill_id",
                table: "cash_flow_input");

            migrationBuilder.DropIndex(
                name: "ux_cash_flow_input_contribution_loan_payment",
                table: "cash_flow_input");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cash_flow_input_contribution_loan",
                table: "cash_flow_input");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cash_flow_input_contribution_one_source",
                table: "cash_flow_input");

            migrationBuilder.DropIndex(
                name: "ix_bill_contribution_account_id",
                table: "bill");

            migrationBuilder.DropIndex(
                name: "ix_bill_share_participant_id",
                table: "bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_share",
                table: "bill");

            migrationBuilder.DropColumn(
                name: "contribution_account_id",
                table: "recurring_bill");

            migrationBuilder.DropColumn(
                name: "participant_share_count",
                table: "recurring_bill");

            migrationBuilder.DropColumn(
                name: "share_count",
                table: "recurring_bill");

            migrationBuilder.DropColumn(
                name: "share_participant_id",
                table: "recurring_bill");

            migrationBuilder.DropColumn(
                name: "contribution_account_id",
                table: "loan_payment");

            migrationBuilder.DropColumn(
                name: "has_own_share",
                table: "loan_payment");

            migrationBuilder.DropColumn(
                name: "is_share_changed",
                table: "loan_payment");

            migrationBuilder.DropColumn(
                name: "participant_share_count",
                table: "loan_payment");

            migrationBuilder.DropColumn(
                name: "share_count",
                table: "loan_payment");

            migrationBuilder.DropColumn(
                name: "share_participant_id",
                table: "loan_payment");

            migrationBuilder.DropColumn(
                name: "contribution_account_id",
                table: "loan");

            migrationBuilder.DropColumn(
                name: "participant_share_count",
                table: "loan");

            migrationBuilder.DropColumn(
                name: "share_count",
                table: "loan");

            migrationBuilder.DropColumn(
                name: "share_participant_id",
                table: "loan");

            migrationBuilder.DropColumn(
                name: "contribution_bill_id",
                table: "cash_flow_input");

            migrationBuilder.DropColumn(
                name: "contribution_loan_id",
                table: "cash_flow_input");

            migrationBuilder.DropColumn(
                name: "contribution_month",
                table: "cash_flow_input");

            migrationBuilder.DropColumn(
                name: "contribution_payment_kind",
                table: "cash_flow_input");

            migrationBuilder.DropColumn(
                name: "is_edited",
                table: "cash_flow_input");

            migrationBuilder.DropColumn(
                name: "contribution_account_id",
                table: "bill");

            migrationBuilder.DropColumn(
                name: "is_share_changed",
                table: "bill");

            migrationBuilder.DropColumn(
                name: "participant_share_count",
                table: "bill");

            migrationBuilder.DropColumn(
                name: "share_count",
                table: "bill");

            migrationBuilder.DropColumn(
                name: "share_participant_id",
                table: "bill");
        }
    }
}
