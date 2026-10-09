using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Expenses;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("expense_category");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(ExpenseCategory.NameMaxLength);
        builder.Ignore(c => c.IsHidden);
        // Name uniqueness ignoring case is a unique index on lower(name), and the starting categories are
        // inserted with SQL, both in the migration: a user's own category may already have a starting name.
    }
}

internal sealed class RecurringBillConfiguration : IEntityTypeConfiguration<RecurringBill>
{
    public void Configure(EntityTypeBuilder<RecurringBill> builder)
    {
        builder.ToTable("recurring_bill");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(RecurringBill.NameMaxLength);
        builder.Property(r => r.Payee).HasMaxLength(RecurringBill.PayeeMaxLength);
        builder.Property(r => r.Description).HasMaxLength(RecurringBill.DescriptionMaxLength);
        builder.Ignore(r => r.IsActive);
        builder.Ignore(r => r.Details);

        builder.HasOne<ExpenseCategory>().WithMany().HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_recurring_bill_expense_category_category_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(r => r.PaidFromId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_recurring_bill_card_paid_from_id");

        builder.HasIndex(r => r.CategoryId).HasDatabaseName("ix_recurring_bill_category_id");
        builder.HasIndex(r => r.PaidFromId).HasDatabaseName("ix_recurring_bill_paid_from_id");
        // Names are unique among active recurring bills ignoring case: a partial index on lower(name),
        // created in the migration.
    }
}

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bill", table =>
        {
            table.HasCheckConstraint("ck_bill_dates", "(issue_date IS NULL) = (due_date IS NULL)");
            table.HasCheckConstraint(
                "ck_bill_amount", "(issue_date IS NULL AND amount_cents = 0) OR (issue_date IS NOT NULL AND amount_cents > 0)");
            table.HasCheckConstraint("ck_bill_due_after_issue", "due_date >= issue_date");
            table.HasCheckConstraint("ck_bill_paid", "is_paid = (paid_date IS NOT NULL)");
            table.HasCheckConstraint("ck_bill_paid_issued", "NOT is_paid OR issue_date IS NOT NULL");
            table.HasCheckConstraint("ck_bill_paid_from", "NOT is_paid OR paid_from_id IS NOT NULL");
            table.HasCheckConstraint("ck_bill_paid_after_issue", "paid_date >= issue_date");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Name).HasMaxLength(RecurringBill.NameMaxLength);
        builder.Property(b => b.Payee).HasMaxLength(RecurringBill.PayeeMaxLength);
        builder.Property(b => b.Description).HasMaxLength(RecurringBill.DescriptionMaxLength);
        builder.Property(b => b.Amount).HasColumnName("amount_cents");
        builder.Ignore(b => b.IsIssued);
        builder.Ignore(b => b.State);
        builder.Ignore(b => b.CashFlowDate);
        builder.Ignore(b => b.Details);

        builder.HasOne<RecurringBill>().WithMany().HasForeignKey(b => b.RecurringBillId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bill_recurring_bill_recurring_bill_id");
        builder.HasOne<ExpenseCategory>().WithMany().HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bill_expense_category_category_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(b => b.PaidFromId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bill_card_paid_from_id");

        builder.HasIndex(b => new { b.RecurringBillId, b.Month }).IsUnique()
            .HasFilter("recurring_bill_id IS NOT NULL")
            .HasDatabaseName("ux_bill_recurring_bill_id_month");
        builder.HasIndex(b => b.Month).HasDatabaseName("ix_bill_month");
        builder.HasIndex(b => b.CategoryId).HasDatabaseName("ix_bill_category_id");
        builder.HasIndex(b => new { b.PaidFromId, b.PaidDate }).HasDatabaseName("ix_bill_paid_from_id_paid_date");
    }
}

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expense", table => table.HasCheckConstraint("ck_expense_amount_positive", "amount_cents > 0"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Description).HasMaxLength(Expense.DescriptionMaxLength);
        builder.Property(e => e.Amount).HasColumnName("amount_cents");
        builder.Ignore(e => e.IsCashWithdrawal);
        builder.Ignore(e => e.Details);

        builder.HasOne<ExpenseCategory>().WithMany().HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_expense_expense_category_category_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(e => e.PaidFromId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_expense_card_paid_from_id");

        builder.HasIndex(e => e.Date).HasDatabaseName("ix_expense_date");
        builder.HasIndex(e => e.CategoryId).HasDatabaseName("ix_expense_category_id");
        builder.HasIndex(e => new { e.PaidFromId, e.Date }).HasDatabaseName("ix_expense_paid_from_id_date");
    }
}
