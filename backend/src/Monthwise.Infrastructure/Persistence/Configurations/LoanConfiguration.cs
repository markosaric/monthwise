using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Loans;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loan", table =>
        {
            table.HasCheckConstraint("ck_loan_monthly_payment_positive", "monthly_payment_cents > 0");
            table.HasCheckConstraint("ck_loan_residual_value_positive", "residual_value_cents IS NULL OR residual_value_cents > 0");
            table.HasCheckConstraint("ck_loan_payment_day", "payment_day BETWEEN 1 AND 31");
            table.HasCheckConstraint("ck_loan_payment_count", "payment_count >= 1");
            table.HasCheckConstraint(
                "ck_loan_standing_order_account", "(payment_method = 'StandingOrder') = (account_id IS NOT NULL)");
            table.HasCheckConstraint("ck_loan_share", ShareColumnsConfiguration.ShareCheck());
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(Loan.NameMaxLength);
        builder.Property(l => l.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.MonthlyPayment).HasColumnName("monthly_payment_cents");
        builder.Property(l => l.ResidualValue).HasColumnName("residual_value_cents");
        builder.Property(l => l.Description).HasMaxLength(Loan.DescriptionMaxLength);
        builder.Ignore(l => l.IsClosed);
        builder.Ignore(l => l.EndMonth);
        builder.Ignore(l => l.LastPaymentMonth);
        builder.Ignore(l => l.Details);
        builder.ConfigureShare("loan");

        builder.HasOne<LoanCategory>().WithMany().HasForeignKey(l => l.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_loan_loan_category_category_id");
        builder.HasOne<LoanSubcategory>().WithMany().HasForeignKey(l => l.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_loan_loan_subcategory_subcategory_id");
        builder.HasOne<Bank>().WithMany().HasForeignKey(l => l.LenderId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_loan_bank_lender_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_loan_card_account_id");

        builder.HasMany(l => l.Changes).WithOne().HasForeignKey(c => c.LoanId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_loan_payment_change_loan_loan_id");
        builder.Navigation(l => l.Changes).HasField("_changes").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(l => l.CategoryId).HasDatabaseName("ix_loan_category_id");
        builder.HasIndex(l => l.SubcategoryId).HasDatabaseName("ix_loan_subcategory_id");
        builder.HasIndex(l => l.LenderId).HasDatabaseName("ix_loan_lender_id");
        builder.HasIndex(l => l.AccountId).HasDatabaseName("ix_loan_account_id");
        // Name uniqueness ignoring case among loans that are not closed is a partial unique index on
        // lower(name), created in the migration.
    }
}

internal sealed class PaymentChangeConfiguration : IEntityTypeConfiguration<PaymentChange>
{
    public void Configure(EntityTypeBuilder<PaymentChange> builder)
    {
        builder.ToTable("loan_payment_change", table =>
        {
            table.HasCheckConstraint(
                "ck_loan_payment_change_not_empty", "monthly_payment_cents IS NOT NULL OR payment_day IS NOT NULL");
            table.HasCheckConstraint(
                "ck_loan_payment_change_monthly_payment_positive", "monthly_payment_cents IS NULL OR monthly_payment_cents > 0");
            table.HasCheckConstraint(
                "ck_loan_payment_change_payment_day", "payment_day IS NULL OR payment_day BETWEEN 1 AND 31");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.MonthlyPayment).HasColumnName("monthly_payment_cents");
        builder.Property(c => c.Note).HasMaxLength(PaymentChange.NoteMaxLength);
        builder.Ignore(c => c.Details);
        builder.HasIndex(c => new { c.LoanId, c.FromMonth }).IsUnique().HasDatabaseName("ux_loan_payment_change_loan_id_from_month");
    }
}

internal sealed class LoanPaymentConfiguration : IEntityTypeConfiguration<LoanPayment>
{
    public void Configure(EntityTypeBuilder<LoanPayment> builder)
    {
        builder.ToTable("loan_payment", table =>
        {
            table.HasCheckConstraint("ck_loan_payment_amount_positive", "amount_cents IS NULL OR amount_cents > 0");
            table.HasCheckConstraint("ck_loan_payment_paid", "(is_paid IS TRUE) = (paid_date IS NOT NULL)");
            table.HasCheckConstraint("ck_loan_payment_paid_account", "(is_paid IS TRUE) = (paid_account_id IS NOT NULL)");
            table.HasCheckConstraint("ck_loan_payment_paid_after_bill", "paid_date IS NULL OR paid_date >= month");
            table.HasCheckConstraint("ck_loan_payment_share", ShareColumnsConfiguration.ShareCheck("has_own_share"));
            table.HasCheckConstraint("ck_loan_payment_share_changed", "NOT is_share_changed OR has_own_share");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Amount).HasColumnName("amount_cents");
        builder.Ignore(p => p.IsEmpty);
        builder.ConfigureShare("loan_payment");

        builder.HasOne<Loan>().WithMany().HasForeignKey(p => p.LoanId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_loan_payment_loan_loan_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(p => p.PaidAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_loan_payment_card_paid_account_id");

        builder.HasIndex(p => new { p.LoanId, p.Month, p.Kind }).IsUnique().HasDatabaseName("ux_loan_payment_loan_id_month_kind");
        builder.HasIndex(p => p.PaidDate).HasDatabaseName("ix_loan_payment_paid_date");
        builder.HasIndex(p => p.DueDate).HasDatabaseName("ix_loan_payment_due_date");
        builder.HasIndex(p => p.PaidAccountId).HasDatabaseName("ix_loan_payment_paid_account_id");
    }
}

internal sealed class LoanCategoryConfiguration : IEntityTypeConfiguration<LoanCategory>
{
    public void Configure(EntityTypeBuilder<LoanCategory> builder)
    {
        builder.ToTable("loan_category");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(LoanCategory.NameMaxLength);
        builder.Ignore(c => c.IsHidden);
        builder.HasData(LoanSeedData.Categories.Select(c => new
        {
            c.Id,
            c.Name,
            c.AllowsResidualValue,
            IsPredefined = true,
        }));
        // Name uniqueness ignoring case is a unique index on lower(name), created in the migration.
    }
}

internal sealed class LoanSubcategoryConfiguration : IEntityTypeConfiguration<LoanSubcategory>
{
    public void Configure(EntityTypeBuilder<LoanSubcategory> builder)
    {
        builder.ToTable("loan_subcategory");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(LoanSubcategory.NameMaxLength);
        builder.Ignore(s => s.IsHidden);
        builder.HasOne<LoanCategory>().WithMany().HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_loan_subcategory_loan_category_category_id");
        builder.HasIndex(s => s.CategoryId).HasDatabaseName("ix_loan_subcategory_category_id");
        builder.HasData(LoanSeedData.Subcategories.Select(s => new
        {
            s.Id,
            s.CategoryId,
            s.Name,
            IsPredefined = true,
        }));
        // Name uniqueness within the category ignoring case is a unique index on (category_id,
        // lower(name)), created in the migration.
    }
}

/// <summary>Predefined loan categories and subcategories (FR-8). Ids are fixed so migrations stay stable.</summary>
internal static class LoanSeedData
{
    public static readonly Guid BankLoanId = new("0199c2a0-0000-7000-8000-000000000301");
    public static readonly Guid LeaseId = new("0199c2a0-0000-7000-8000-000000000302");

    public static readonly (Guid Id, string Name, bool AllowsResidualValue)[] Categories =
    [
        (BankLoanId, "Bank loan", false),
        (LeaseId, "Lease", true),
    ];

    public static readonly (Guid Id, Guid CategoryId, string Name)[] Subcategories =
    [
        (new Guid("0199c2a0-0000-7000-8000-000000000311"), BankLoanId, "Mortgage"),
        (new Guid("0199c2a0-0000-7000-8000-000000000312"), BankLoanId, "Cash loan"),
        (new Guid("0199c2a0-0000-7000-8000-000000000313"), BankLoanId, "Car loan"),
        (new Guid("0199c2a0-0000-7000-8000-000000000314"), BankLoanId, "Other loan"),
        (new Guid("0199c2a0-0000-7000-8000-000000000321"), LeaseId, "Car lease"),
        (new Guid("0199c2a0-0000-7000-8000-000000000322"), LeaseId, "Equipment lease"),
        (new Guid("0199c2a0-0000-7000-8000-000000000323"), LeaseId, "Other lease"),
    ];
}
