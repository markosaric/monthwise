using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Infrastructure.Persistence.Converters;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("card", table =>
        {
            table.HasCheckConstraint("ck_card_validity_order", "valid_from IS NULL OR valid_through >= valid_from");
            table.HasCheckConstraint("ck_card_installments_debit", "NOT allows_installments OR type = 'Debit'");
            table.HasCheckConstraint("ck_card_debt_limit_not_negative", "debt_limit_cents >= 0");
            table.HasCheckConstraint("ck_card_primary_is_debit", "NOT is_primary OR type = 'Debit'");
            table.HasCheckConstraint("ck_card_number_last4", "card_number_last4 ~ '^[0-9]{4}$'");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Card.NameMaxLength);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.DebtLimit).HasColumnName("debt_limit_cents");
        builder.Property(c => c.AllowsInstallmentsFlag).HasColumnName("allows_installments");
        builder.Ignore(c => c.AllowsInstallments);
        builder.Property(c => c.CardNumberLast4).HasMaxLength(4).IsFixedLength();
        builder.Property(c => c.IbanCountry).HasMaxLength(2).IsFixedLength();
        builder.Property(c => c.IbanLast4).HasMaxLength(4).IsFixedLength();
        builder.Property(c => c.BlockReason).HasMaxLength(Card.ReasonMaxLength);
        builder.Property(c => c.ExpiryReason).HasMaxLength(Card.ReasonMaxLength);

        builder.HasOne<CardIssuer>().WithMany().HasForeignKey(c => c.IssuerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(c => c.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Card>().WithMany().HasForeignKey(c => c.ReplacedByCardId).OnDelete(DeleteBehavior.SetNull);

        // The numbers share the card table but are a separate entity, so they are only read
        // (and decrypted) when a query explicitly includes them.
        builder.HasOne(c => c.SensitiveData).WithOne().HasForeignKey<CardSensitiveData>(d => d.CardId);
        builder.Navigation(c => c.SensitiveData).IsRequired();
        builder.Ignore(c => c.CardNumberMasked);
        builder.Ignore(c => c.IbanMasked);
        builder.Ignore(c => c.IsReadOnly);
        // Name uniqueness ignoring case is a unique index on lower(name), created in the migration.
    }
}

internal sealed class CardSensitiveDataConfiguration(ISensitiveDataCipher cipher) : IEntityTypeConfiguration<CardSensitiveData>
{
    public const string CardNumberHash = "CardNumberHash";
    public const string IbanHash = "IbanHash";

    public void Configure(EntityTypeBuilder<CardSensitiveData> builder)
    {
        builder.ToTable("card");
        builder.HasKey(d => d.CardId);
        builder.Property(d => d.CardId).HasColumnName("id");
        builder.Property(d => d.CardNumber).HasConversion(new EncryptedCardNumberConverter(cipher));
        builder.Property(d => d.Iban).HasConversion(new EncryptedIbanConverter(cipher));
        builder.Property<string?>(CardNumberHash).HasMaxLength(64);
        builder.Property<string?>(IbanHash).HasMaxLength(64);

        // The same number cannot be on two cards that are not archived.
        builder.HasIndex(CardNumberHash).IsUnique().HasFilter("card_number_hash IS NOT NULL AND archived_on IS NULL");
        builder.HasIndex(IbanHash).IsUnique().HasFilter("iban_hash IS NOT NULL AND archived_on IS NULL");
    }
}

internal sealed class CardIssuerConfiguration : IEntityTypeConfiguration<CardIssuer>
{
    public void Configure(EntityTypeBuilder<CardIssuer> builder)
    {
        builder.ToTable("card_issuer");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Name).HasMaxLength(CardListEntry.NameMaxLength);
        builder.HasData(SeedData.Issuers.Select(e => new { e.Id, e.Name, IsPredefined = true }));
    }
}

internal sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("bank");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Name).HasMaxLength(CardListEntry.NameMaxLength);
        builder.HasData(SeedData.Banks.Select(e => new { e.Id, e.Name, IsPredefined = true }));
    }
}

/// <summary>Predefined list entries (requirements A2, A3). Ids are fixed so migrations stay stable.</summary>
internal static class SeedData
{
    public static readonly (Guid Id, string Name)[] Issuers =
    [
        (new Guid("0199b0c0-0000-7000-8000-000000000001"), "Visa"),
        (new Guid("0199b0c0-0000-7000-8000-000000000002"), "Mastercard"),
        (new Guid("0199b0c0-0000-7000-8000-000000000003"), "Diners"),
        (new Guid("0199b0c0-0000-7000-8000-000000000004"), "Maestro"),
        (new Guid("0199b0c0-0000-7000-8000-000000000005"), "American Express"),
    ];

    public static readonly (Guid Id, string Name)[] Banks =
    [
        (new Guid("0199b0c0-0000-7000-8000-000000000101"), "ZaBa"),
        (new Guid("0199b0c0-0000-7000-8000-000000000102"), "PBZ"),
        (new Guid("0199b0c0-0000-7000-8000-000000000103"), "Erste"),
        (new Guid("0199b0c0-0000-7000-8000-000000000104"), "OTP"),
        (new Guid("0199b0c0-0000-7000-8000-000000000105"), "RBA"),
        (new Guid("0199b0c0-0000-7000-8000-000000000106"), "Addiko"),
    ];
}
