using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.Loans;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class CashFlowInputConfiguration : IEntityTypeConfiguration<CashFlowInput>
{
    public void Configure(EntityTypeBuilder<CashFlowInput> builder)
    {
        builder.ToTable("cash_flow_input", table =>
        {
            table.HasCheckConstraint("ck_cash_flow_input_amount_positive", "amount_cents > 0");
            table.HasCheckConstraint("ck_cash_flow_input_expected_after_issue", "expected_date >= issue_date");
            table.HasCheckConstraint(
                "ck_cash_flow_input_received_after_issue", "received_date IS NULL OR received_date >= issue_date");
            table.HasCheckConstraint(
                "ck_cash_flow_input_occurrence", "(recurring_input_id IS NULL) = (occurrence_date IS NULL)");
            table.HasCheckConstraint(
                "ck_cash_flow_input_contribution_loan",
                "(contribution_loan_id IS NULL) = (contribution_month IS NULL) AND (contribution_loan_id IS NULL) = (contribution_payment_kind IS NULL)");
            table.HasCheckConstraint(
                "ck_cash_flow_input_contribution_one_source", "contribution_bill_id IS NULL OR contribution_loan_id IS NULL");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Amount).HasColumnName("amount_cents");
        builder.Property(i => i.Description).HasMaxLength(CashFlowInput.DescriptionMaxLength);
        builder.Ignore(i => i.IsReceived);
        builder.Ignore(i => i.MonthDate);
        builder.Ignore(i => i.Month);
        builder.Ignore(i => i.Details);
        builder.Ignore(i => i.Contribution);
        builder.Property(i => i.ContributionPaymentKind).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Payer>().WithMany().HasForeignKey(i => i.PayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InputCategory>().WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Card>().WithMany().HasForeignKey(i => i.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RecurringInput>().WithMany().HasForeignKey(i => i.RecurringInputId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bill>().WithMany().HasForeignKey(i => i.ContributionBillId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cash_flow_input_bill_contribution_bill_id");
        builder.HasOne<Loan>().WithMany().HasForeignKey(i => i.ContributionLoanId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cash_flow_input_loan_contribution_loan_id");

        builder.HasIndex(i => new { i.RecurringInputId, i.OccurrenceDate }).IsUnique().HasFilter("recurring_input_id IS NOT NULL");
        builder.HasIndex(i => i.ContributionBillId).IsUnique().HasFilter("contribution_bill_id IS NOT NULL")
            .HasDatabaseName("ux_cash_flow_input_contribution_bill_id");
        builder.HasIndex(i => new { i.ContributionLoanId, i.ContributionMonth, i.ContributionPaymentKind }).IsUnique()
            .HasFilter("contribution_loan_id IS NOT NULL")
            .HasDatabaseName("ux_cash_flow_input_contribution_loan_payment");
        builder.HasIndex(i => i.ReceivedDate);
        builder.HasIndex(i => i.ExpectedDate);
    }
}

internal sealed class RecurringInputConfiguration : IEntityTypeConfiguration<RecurringInput>
{
    public void Configure(EntityTypeBuilder<RecurringInput> builder)
    {
        builder.ToTable("recurring_input", table =>
        {
            table.HasCheckConstraint("ck_recurring_input_amount_positive", "amount_cents > 0");
            table.HasCheckConstraint("ck_recurring_input_expected_after_issue", "expected_anchor >= issue_anchor");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Amount).HasColumnName("amount_cents");
        builder.Property(r => r.Description).HasMaxLength(CashFlowInput.DescriptionMaxLength);
        builder.Property(r => r.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(r => r.Schedule);
        builder.Ignore(r => r.Details);

        builder.HasOne<Payer>().WithMany().HasForeignKey(r => r.PayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InputCategory>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Card>().WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SkippedOccurrenceConfiguration : IEntityTypeConfiguration<SkippedOccurrence>
{
    public void Configure(EntityTypeBuilder<SkippedOccurrence> builder)
    {
        builder.ToTable("skipped_occurrence");
        builder.HasKey(s => new { s.RecurringInputId, s.OccurrenceDate });
        builder.HasOne<RecurringInput>().WithMany().HasForeignKey(s => s.RecurringInputId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PayerConfiguration : IEntityTypeConfiguration<Payer>
{
    public void Configure(EntityTypeBuilder<Payer> builder)
    {
        builder.ToTable("payer");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Payer.NameMaxLength);
        builder.Ignore(p => p.IsRemoved);
        // Name uniqueness ignoring case is a unique index on lower(name), created in the migration.
    }
}

internal sealed class InputCategoryConfiguration : IEntityTypeConfiguration<InputCategory>
{
    public void Configure(EntityTypeBuilder<InputCategory> builder)
    {
        builder.ToTable("input_category");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(InputCategory.NameMaxLength);
        builder.Ignore(c => c.IsHidden);
        builder.HasData(CashFlowSeedData.Categories.Select(c => new { c.Id, c.Name, IsPredefined = true }));
        // Name uniqueness ignoring case is a unique index on lower(name), created in the migration.
    }
}

internal sealed class BalanceEntryConfiguration : IEntityTypeConfiguration<BalanceEntry>
{
    public void Configure(EntityTypeBuilder<BalanceEntry> builder)
    {
        builder.ToTable("balance_entry");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Balance).HasColumnName("balance_cents");
        builder.Property(e => e.Note).HasMaxLength(BalanceEntry.NoteMaxLength);
        builder.HasOne<Card>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AccountId, e.Date });
        // One starting balance per account.
        builder.HasIndex(e => e.AccountId).IsUnique().HasFilter("kind = 'Starting'").HasDatabaseName("ux_balance_entry_one_starting");
    }
}

/// <summary>Predefined input categories (FR-6). Ids are fixed so migrations stay stable.</summary>
internal static class CashFlowSeedData
{
    public static readonly (Guid Id, string Name)[] Categories =
    [
        (new Guid("0199b0c0-0000-7000-8000-000000000201"), "Salary"),
        (new Guid("0199b0c0-0000-7000-8000-000000000202"), "Salary add-ons"),
        (new Guid("0199b0c0-0000-7000-8000-000000000203"), "Agreed contributions"),
        (new Guid("0199b0c0-0000-7000-8000-000000000204"), "Other income"),
    ];
}
