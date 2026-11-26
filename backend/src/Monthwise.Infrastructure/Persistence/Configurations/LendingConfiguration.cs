using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Lendings;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class LendingConfiguration : IEntityTypeConfiguration<Lending>
{
    public void Configure(EntityTypeBuilder<Lending> builder)
    {
        builder.ToTable("lending", table =>
        {
            table.HasCheckConstraint("ck_lending_amount_positive", "amount_cents > 0");
            table.HasCheckConstraint("ck_lending_return_after_lend", "expected_return_date >= lend_date");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Amount).HasColumnName("amount_cents");
        builder.Property(l => l.Description).HasMaxLength(Lending.DescriptionMaxLength);
        builder.Ignore(l => l.Details);
        builder.Ignore(l => l.Returned);
        builder.Ignore(l => l.Outstanding);

        builder.HasOne<Payer>().WithMany().HasForeignKey(l => l.PayerId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_lending_payer_payer_id");
        builder.HasOne<Card>().WithMany().HasForeignKey(l => l.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_lending_card_from_account_id");

        builder.HasMany(l => l.Repayments).WithOne().HasForeignKey(r => r.LendingId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_lending_repayment_lending_lending_id");
        builder.Navigation(l => l.Repayments).HasField("_repayments").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(l => l.PayerId).HasDatabaseName("ix_lending_payer_id");
        builder.HasIndex(l => new { l.FromAccountId, l.LendDate }).HasDatabaseName("ix_lending_from_account_id_lend_date");
    }
}

internal sealed class LendingRepaymentConfiguration : IEntityTypeConfiguration<LendingRepayment>
{
    public void Configure(EntityTypeBuilder<LendingRepayment> builder)
    {
        builder.ToTable("lending_repayment", table => table.HasCheckConstraint("ck_lending_repayment_amount_positive", "amount_cents > 0"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Amount).HasColumnName("amount_cents");
        builder.Property(r => r.Note).HasMaxLength(LendingRepayment.NoteMaxLength);
        builder.Ignore(r => r.Details);

        builder.HasOne<Card>().WithMany().HasForeignKey(r => r.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_lending_repayment_card_to_account_id");

        builder.HasIndex(r => r.LendingId).HasDatabaseName("ix_lending_repayment_lending_id");
        builder.HasIndex(r => new { r.ToAccountId, r.ReceivedDate }).HasDatabaseName("ix_lending_repayment_to_account_id_received_date");
    }
}
