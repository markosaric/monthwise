using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.Users;
using Monthwise.Infrastructure.Users;

namespace Monthwise.Infrastructure.Persistence.Configurations;

/// <summary>
/// The login tables (login.md, ADR 0008). All are installation tables: never exported, never replaced
/// by an import (see <see cref="DataTransfer.HouseholdDataStore.InstallationTables"/>).
/// </summary>
internal static class LoginConfiguration
{
    public static void ApplyLoginConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(user =>
        {
            user.ToTable("app_user");
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.DisplayName).HasMaxLength(UserRules.DisplayNameMaxLength);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_app_user_normalized_user_name");
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_app_user_normalized_email");
        });
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("app_user_claim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("app_user_login");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("app_user_token");
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_key");
        modelBuilder.ApplyConfiguration(new SetupCodeUseConfiguration());
    }

    private sealed class SetupCodeUseConfiguration : IEntityTypeConfiguration<SetupCodeUse>
    {
        public void Configure(EntityTypeBuilder<SetupCodeUse> builder)
        {
            builder.ToTable("setup_code_use");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedNever();
            builder.Property(u => u.CodeHash).HasMaxLength(64);
            builder.HasIndex(u => u.CodeHash).IsUnique();
        }
    }
}
