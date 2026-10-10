using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("participant");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Participant.NameMaxLength);
        builder.Property(p => p.Description).HasMaxLength(Participant.DescriptionMaxLength);
        builder.Ignore(p => p.IsArchived);

        builder.HasOne<Payer>().WithMany().HasForeignKey(p => p.PayerId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_participant_payer_payer_id");
        builder.HasIndex(p => p.PayerId).IsUnique().HasDatabaseName("ux_participant_payer_id");
        // Name uniqueness ignoring case is a unique index on lower(name), created in the migration.
    }
}

/// <summary>The four share columns of a shared cost (shared bills FR-2) and their foreign keys and checks.</summary>
internal static class ShareColumnsConfiguration
{
    /// <summary>All share columns set, or none (the contribution account is optional); 2 to 4 shares.</summary>
    public static string ShareCheck(string? sharedCondition = null) =>
        "(share_participant_id IS NULL AND share_count IS NULL AND participant_share_count IS NULL AND contribution_account_id IS NULL) OR "
        + "(share_participant_id IS NOT NULL AND share_count BETWEEN 2 AND 4 "
        + "AND participant_share_count >= 1 AND participant_share_count < share_count"
        + (sharedCondition is null ? ")" : " AND " + sharedCondition + ")");

    public static void ConfigureShare<T>(this EntityTypeBuilder<T> builder, string table)
        where T : class
    {
        builder.Ignore("Share");
        builder.HasOne<Participant>().WithMany().HasForeignKey("ShareParticipantId")
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName($"fk_{table}_participant_share_participant_id");
        builder.HasOne<Card>().WithMany().HasForeignKey("ContributionAccountId")
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName($"fk_{table}_card_contribution_account_id");
        builder.HasIndex("ShareParticipantId").HasDatabaseName($"ix_{table}_share_participant_id");
        builder.HasIndex("ContributionAccountId").HasDatabaseName($"ix_{table}_contribution_account_id");
    }
}
