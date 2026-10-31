using Microsoft.EntityFrameworkCore;
using Monthwise.Application.DataTransfer;
using Monthwise.Domain.DataTransfer;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class DataTransferRepository(MonthwiseDbContext dbContext) : IDataTransferRepository
{
    public void Add(DataTransferRecord record) => dbContext.DataTransferLog.Add(record);

    public void Add(SafetyCopy safetyCopy) => dbContext.SafetyCopies.Add(safetyCopy);

    public Task<DataTransferRecord?> GetLastSuccessfulAsync(
        IReadOnlyCollection<DataTransferKind> kinds, CancellationToken cancellationToken) =>
        dbContext.DataTransferLog.AsNoTracking()
            .Where(r => r.Succeeded && kinds.Contains(r.Kind))
            .OrderByDescending(r => r.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<DataTransferRecord> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.DataTransferLog.CountAsync(cancellationToken);
        var items = await dbContext.DataTransferLog.AsNoTracking()
            .OrderByDescending(r => r.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<BackupReminder> GetReminderAsync(CancellationToken cancellationToken)
    {
        var reminder = await dbContext.BackupReminders.FirstOrDefaultAsync(cancellationToken);
        if (reminder is null)
        {
            // The migration adds the row; this covers a database where it was removed.
            reminder = BackupReminder.CreateDefault();
            dbContext.BackupReminders.Add(reminder);
        }
        return reminder;
    }

    public Task<SafetyCopy?> GetSafetyCopyAsync(CancellationToken cancellationToken) =>
        dbContext.SafetyCopies.FirstOrDefaultAsync(cancellationToken);

    public Task<DateTimeOffset?> GetSafetyCopyCreatedAtAsync(CancellationToken cancellationToken) =>
        dbContext.SafetyCopies.AsNoTracking().Select(c => (DateTimeOffset?)c.CreatedAt).FirstOrDefaultAsync(cancellationToken);
}
