using Monthwise.Domain.Common;

namespace Monthwise.Domain.DataTransfer;

/// <summary>
/// The export reminder on the Dashboard and its setting (backup and restore FR-10, FR-11). One per
/// installation; it applies to the whole household and is not part of the exported data.
/// </summary>
public sealed class BackupReminder
{
    public const int SingletonId = 1;
    public const int DefaultIntervalDays = 30;
    public static readonly IReadOnlyList<int> IntervalChoices = [7, 14, 30, 60, 90];

    private BackupReminder()
    {
    }

    public int Id { get; private set; }
    public bool Enabled { get; private set; }
    public int IntervalDays { get; private set; }

    /// <summary>When the reminder was last dismissed; it stays hidden for one interval after that.</summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    public static BackupReminder CreateDefault() =>
        new() { Id = SingletonId, Enabled = true, IntervalDays = DefaultIntervalDays };

    public Result Change(bool enabled, int intervalDays)
    {
        if (!IntervalChoices.Contains(intervalDays))
        {
            return Result.Failure(DataTransferErrors.ReminderIntervalInvalid);
        }
        Enabled = enabled;
        IntervalDays = intervalDays;
        return Result.Success();
    }

    public void Dismiss(DateTimeOffset now) => DismissedAt = now;

    /// <summary>
    /// Due while the reminder is on, no successful export is newer than the interval, and it was
    /// not dismissed within the last interval.
    /// </summary>
    public bool IsDue(DateTimeOffset now, DateTimeOffset? lastExportAt)
    {
        var interval = TimeSpan.FromDays(IntervalDays);
        return Enabled
            && (lastExportAt is null || now - lastExportAt.Value >= interval)
            && (DismissedAt is null || now - DismissedAt.Value >= interval);
    }
}
