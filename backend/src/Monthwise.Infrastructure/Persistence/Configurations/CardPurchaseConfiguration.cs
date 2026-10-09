using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class CardBillingTermsConfiguration : IEntityTypeConfiguration<CardBillingTerms>
{
    public void Configure(EntityTypeBuilder<CardBillingTerms> builder)
    {
        builder.ToTable("card_billing_terms", table =>
        {
            table.HasCheckConstraint("ck_card_billing_terms_statement_day", "statement_day BETWEEN 1 AND 30");
            table.HasCheckConstraint("ck_card_billing_terms_payment_day", "payment_day BETWEEN 2 AND 31");
            table.HasCheckConstraint("ck_card_billing_terms_payment_after_statement", "payment_day > statement_day");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.HasIndex(t => new { t.CardId, t.FromMonth }).IsUnique().HasDatabaseName("ux_card_billing_terms_card_id_from_month");
    }
}

internal sealed class PurchaseCategoryConfiguration : IEntityTypeConfiguration<PurchaseCategory>
{
    public void Configure(EntityTypeBuilder<PurchaseCategory> builder)
    {
        builder.ToTable("purchase_category");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(PurchaseCategory.NameMaxLength);
        builder.Ignore(c => c.IsHidden);
        // Name uniqueness ignoring case is a unique index on lower(name), created in the migration.
    }
}

internal sealed class CardPurchaseConfiguration : IEntityTypeConfiguration<CardPurchase>
{
    public void Configure(EntityTypeBuilder<CardPurchase> builder)
    {
        builder.ToTable("card_purchase", table =>
        {
            table.HasCheckConstraint("ck_card_purchase_amount_positive", "amount_cents > 0");
            table.HasCheckConstraint("ck_card_purchase_installment_count", "installment_count BETWEEN 1 AND 36");
            table.HasCheckConstraint("ck_card_purchase_postpone_installments", "NOT postpone_first OR installment_count > 1");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Description).HasMaxLength(CardPurchase.DescriptionMaxLength);
        builder.Property(p => p.Amount).HasColumnName("amount_cents");
        builder.Property(p => p.ReversalReason).HasMaxLength(CardPurchase.ReasonMaxLength);
        builder.Ignore(p => p.InInstallments);
        builder.Ignore(p => p.IsReversed);
        builder.Ignore(p => p.Details);

        builder.HasOne<Card>().WithMany().HasForeignKey(p => p.CardId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_card_purchase_card_card_id");
        builder.HasOne<PurchaseCategory>().WithMany().HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_card_purchase_purchase_category_category_id");

        builder.HasMany(p => p.Reversals).WithOne().HasForeignKey(r => r.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_card_purchase_reversal_card_purchase_purchase_id");
        builder.Navigation(p => p.Reversals).HasField("_reversals").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(p => new { p.CardId, p.PurchaseDate }).HasDatabaseName("ix_card_purchase_card_id_purchase_date");
        builder.HasIndex(p => p.CategoryId).HasDatabaseName("ix_card_purchase_category_id");
    }
}

internal sealed class InstallmentReversalConfiguration : IEntityTypeConfiguration<InstallmentReversal>
{
    public void Configure(EntityTypeBuilder<InstallmentReversal> builder)
    {
        builder.ToTable("card_purchase_reversal", table =>
            table.HasCheckConstraint("ck_card_purchase_reversal_installment_number", "installment_number >= 1"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Reason).HasMaxLength(CardPurchase.ReasonMaxLength);
        builder.HasIndex(r => new { r.PurchaseId, r.InstallmentNumber }).IsUnique()
            .HasDatabaseName("ux_card_purchase_reversal_purchase_id_installment_number");
    }
}

internal sealed class CardPaymentConfiguration : IEntityTypeConfiguration<CardPayment>
{
    public void Configure(EntityTypeBuilder<CardPayment> builder)
    {
        builder.ToTable("card_payment", table =>
        {
            table.HasCheckConstraint("ck_card_payment_paid", "(is_paid IS TRUE) = (paid_date IS NOT NULL)");
            table.HasCheckConstraint("ck_card_payment_paid_account", "(is_paid IS TRUE) = (paid_account_id IS NOT NULL)");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.IsEmpty);

        builder.HasOne<Card>().WithMany().HasForeignKey(p => p.CardId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_card_payment_card_card_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(p => p.PaidAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_card_payment_card_paid_account_id");

        builder.HasIndex(p => new { p.CardId, p.Month }).IsUnique().HasDatabaseName("ux_card_payment_card_id_month");
        builder.HasIndex(p => p.PaidAccountId).HasDatabaseName("ix_card_payment_paid_account_id");
    }
}
