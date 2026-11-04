using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBillOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_amount",
                table: "bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_dates",
                table: "bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_paid_issued",
                table: "bill");

            migrationBuilder.AddColumn<long>(
                name: "amount_cents",
                table: "recurring_bill",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "due_day",
                table: "recurring_bill",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "issue_day",
                table: "recurring_bill",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurring_bill_amount_positive",
                table: "recurring_bill",
                sql: "amount_cents > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurring_bill_due_day",
                table: "recurring_bill",
                sql: "due_day BETWEEN 1 AND 31");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurring_bill_issue_day",
                table: "recurring_bill",
                sql: "issue_day BETWEEN 1 AND 31");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_amount_not_negative",
                table: "bill",
                sql: "amount_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_one_off_issued",
                table: "bill",
                sql: "recurring_bill_id IS NOT NULL OR (amount_cents > 0 AND issue_date IS NOT NULL AND due_date IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_paid_issued",
                table: "bill",
                sql: "NOT is_paid OR (amount_cents > 0 AND issue_date IS NOT NULL AND due_date IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_recurring_bill_amount_positive",
                table: "recurring_bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recurring_bill_due_day",
                table: "recurring_bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recurring_bill_issue_day",
                table: "recurring_bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_amount_not_negative",
                table: "bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_one_off_issued",
                table: "bill");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_paid_issued",
                table: "bill");

            migrationBuilder.DropColumn(
                name: "amount_cents",
                table: "recurring_bill");

            migrationBuilder.DropColumn(
                name: "due_day",
                table: "recurring_bill");

            migrationBuilder.DropColumn(
                name: "issue_day",
                table: "recurring_bill");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_amount",
                table: "bill",
                sql: "(issue_date IS NULL AND amount_cents = 0) OR (issue_date IS NOT NULL AND amount_cents > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_dates",
                table: "bill",
                sql: "(issue_date IS NULL) = (due_date IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_paid_issued",
                table: "bill",
                sql: "NOT is_paid OR issue_date IS NOT NULL");
        }
    }
}
