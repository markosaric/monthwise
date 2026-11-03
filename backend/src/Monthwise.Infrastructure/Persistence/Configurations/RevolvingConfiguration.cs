using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Revolving;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class RevolvingTermsConfiguration : IEntityTypeConfiguration<RevolvingTerms>
{
    public void Configure(EntityTypeBuilder<RevolvingTerms> builder)
    {
        builder.ToTable("revolving_terms", table =>
        {
            table.HasCheckConstraint("ck_revolving_terms_min_payment_bp", "min_payment_bp BETWEEN 1 AND 10000");
            table.HasCheckConstraint("ck_revolving_terms_min_payment_cents", "min_payment_cents >= 0");
            table.HasCheckConstraint("ck_revolving_terms_interest_bp", "interest_bp >= 0");
            table.HasCheckConstraint(
                "ck_revolving_terms_min_payment_interest_bp",
                "min_payment_interest_bp IS NULL OR min_payment_interest_bp BETWEEN 0 AND min_payment_bp");
            table.HasCheckConstraint("ck_revolving_terms_bill_day", "bill_day BETWEEN 1 AND 31");
            table.HasCheckConstraint("ck_revolving_terms_due_day", "due_day BETWEEN 1 AND 31");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.MinPaymentBasisPoints).HasColumnName("min_payment_bp");
        builder.Property(t => t.MinPaymentAmount).HasColumnName("min_payment_cents");
        builder.Property(t => t.InterestBasisPoints).HasColumnName("interest_bp");
        builder.Property(t => t.MinPaymentInterestBasisPoints).HasColumnName("min_payment_interest_bp");
        builder.HasIndex(t => new { t.CardId, t.FromMonth }).IsUnique().HasDatabaseName("ux_revolving_terms_card_id_from_month");
    }
}

internal sealed class DebtEntryConfiguration : IEntityTypeConfiguration<DebtEntry>
{
    public void Configure(EntityTypeBuilder<DebtEntry> builder)
    {
        builder.ToTable("revolving_debt_entry", table =>
        {
            table.HasCheckConstraint("ck_revolving_debt_entry_amount", "amount_cents >= 0");
            table.HasCheckConstraint(
                "ck_revolving_debt_entry_transfer_linked",
                "(kind IN ('TransferIn', 'TransferOut')) = (linked_card_id IS NOT NULL)");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Amount).HasColumnName("amount_cents");
        builder.Property(e => e.Note).HasMaxLength(DebtEntry.NoteMaxLength);
        builder.Ignore(e => e.IsTransfer);

        builder.HasOne<Card>().WithMany().HasForeignKey(e => e.CardId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_revolving_debt_entry_card_card_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(e => e.LinkedCardId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_revolving_debt_entry_card_linked_card_id");
        builder.HasIndex(e => new { e.CardId, e.Date }).HasDatabaseName("ix_revolving_debt_entry_card_id_date");
        builder.HasIndex(e => e.LinkedCardId).HasDatabaseName("ix_revolving_debt_entry_linked_card_id");
    }
}

internal sealed class ExtraPaymentConfiguration : IEntityTypeConfiguration<ExtraPayment>
{
    public void Configure(EntityTypeBuilder<ExtraPayment> builder)
    {
        builder.ToTable("revolving_extra_payment", table =>
            table.HasCheckConstraint("ck_revolving_extra_payment_amount_positive", "amount_cents > 0"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Amount).HasColumnName("amount_cents");
        builder.Property(p => p.Description).HasMaxLength(ExtraPayment.DescriptionMaxLength);

        builder.HasOne<Card>().WithMany().HasForeignKey(p => p.CardId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_revolving_extra_payment_card_card_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_revolving_extra_payment_card_account_id");
        builder.HasIndex(p => new { p.CardId, p.Date }).HasDatabaseName("ix_revolving_extra_payment_card_id_date");
        builder.HasIndex(p => p.AccountId).HasDatabaseName("ix_revolving_extra_payment_account_id");
    }
}

internal sealed class RevolvingBillConfiguration : IEntityTypeConfiguration<RevolvingBill>
{
    public void Configure(EntityTypeBuilder<RevolvingBill> builder)
    {
        builder.ToTable("revolving_bill", table =>
        {
            table.HasCheckConstraint("ck_revolving_bill_paid", "(is_paid IS TRUE) = (paid_date IS NOT NULL)");
            table.HasCheckConstraint("ck_revolving_bill_paid_account", "(is_paid IS TRUE) = (paid_account_id IS NOT NULL)");
            table.HasCheckConstraint("ck_revolving_bill_paid_amount", "(is_paid IS TRUE) = (paid_amount_cents IS NOT NULL)");
            table.HasCheckConstraint("ck_revolving_bill_paid_amount_positive", "paid_amount_cents > 0");
            table.HasCheckConstraint("ck_revolving_bill_corrections", "minimum_cents >= 0 AND interest_cents >= 0");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Minimum).HasColumnName("minimum_cents");
        builder.Property(b => b.Interest).HasColumnName("interest_cents");
        builder.Property(b => b.PaidAmount).HasColumnName("paid_amount_cents");
        builder.Ignore(b => b.IsCorrected);
        builder.Ignore(b => b.IsEmpty);

        builder.HasOne<Card>().WithMany().HasForeignKey(b => b.CardId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_revolving_bill_card_card_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(b => b.PaidAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_revolving_bill_card_paid_account_id");
        builder.HasIndex(b => new { b.CardId, b.Month }).IsUnique().HasDatabaseName("ux_revolving_bill_card_id_month");
        builder.HasIndex(b => b.PaidAccountId).HasDatabaseName("ix_revolving_bill_paid_account_id");
    }
}
