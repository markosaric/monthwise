using Monthwise.Domain.DataTransfer;

namespace Monthwise.Domain.Tests.DataTransfer;

public class BackupReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Is_on_with_30_days_by_default_and_due_without_any_export()
    {
        var reminder = BackupReminder.CreateDefault();

        Assert.True(reminder.Enabled);
        Assert.Equal(30, reminder.IntervalDays);
        Assert.True(reminder.IsDue(Now, lastExportAt: null));
    }

    [Fact]
    public void Is_due_once_the_last_export_is_older_than_the_interval()
    {
        var reminder = BackupReminder.CreateDefault();

        Assert.False(reminder.IsDue(Now, Now.AddDays(-29)));
        Assert.True(reminder.IsDue(Now, Now.AddDays(-30)));
    }

    [Fact]
    public void Is_never_due_when_turned_off()
    {
        var reminder = BackupReminder.CreateDefault();
        Assert.True(reminder.Change(enabled: false, intervalDays: 7).IsSuccess);

        Assert.False(reminder.IsDue(Now, lastExportAt: null));
    }

    [Fact]
    public void A_dismissed_reminder_comes_back_after_the_next_interval_without_an_export()
    {
        var reminder = BackupReminder.CreateDefault();
        reminder.Change(enabled: true, intervalDays: 7);
        reminder.Dismiss(Now);

        Assert.False(reminder.IsDue(Now.AddDays(6), lastExportAt: null));
        Assert.True(reminder.IsDue(Now.AddDays(7), lastExportAt: null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(365)]
    public void Only_the_offered_intervals_can_be_chosen(int days)
    {
        var reminder = BackupReminder.CreateDefault();

        var result = reminder.Change(enabled: true, intervalDays: days);

        Assert.Equal(DataTransferErrors.ReminderIntervalInvalid, Assert.Single(result.Errors));
        Assert.Equal(30, reminder.IntervalDays);
    }
}
