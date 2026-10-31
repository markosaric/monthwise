using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monthwise.Domain.DataTransfer;

namespace Monthwise.Infrastructure.Persistence.Configurations;

internal sealed class DataTransferRecordConfiguration : IEntityTypeConfiguration<DataTransferRecord>
{
    public void Configure(EntityTypeBuilder<DataTransferRecord> builder)
    {
        builder.ToTable("data_transfer_log");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.UserName).HasMaxLength(DataTransferRecord.UserNameMaxLength);
        builder.Property(r => r.FileName).HasMaxLength(DataTransferRecord.FileNameMaxLength);
        builder.Property(r => r.Release).HasMaxLength(DataTransferRecord.ReleaseMaxLength);
        builder.Property(r => r.Message).HasMaxLength(DataTransferRecord.MessageMaxLength);
        builder.HasIndex(r => r.OccurredAt);
    }
}

internal sealed class BackupReminderConfiguration : IEntityTypeConfiguration<BackupReminder>
{
    public void Configure(EntityTypeBuilder<BackupReminder> builder)
    {
        builder.ToTable("data_backup_settings", table =>
            table.HasCheckConstraint("ck_data_backup_settings_single_row", $"id = {BackupReminder.SingletonId}"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Enabled).HasColumnName("reminder_enabled");
        builder.Property(r => r.IntervalDays).HasColumnName("reminder_interval_days");
        builder.Property(r => r.DismissedAt).HasColumnName("reminder_dismissed_at");
        builder.HasData(new
        {
            Id = BackupReminder.SingletonId,
            Enabled = true,
            IntervalDays = BackupReminder.DefaultIntervalDays,
        });
    }
}

internal sealed class SafetyCopyConfiguration : IEntityTypeConfiguration<SafetyCopy>
{
    public void Configure(EntityTypeBuilder<SafetyCopy> builder)
    {
        builder.ToTable("data_safety_copy", table =>
            table.HasCheckConstraint("ck_data_safety_copy_single_row", $"id = {SafetyCopy.SingletonId}"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Release).HasMaxLength(DataTransferRecord.ReleaseMaxLength);
    }
}
