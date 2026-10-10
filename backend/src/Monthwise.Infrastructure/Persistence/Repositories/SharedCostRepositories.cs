using Microsoft.EntityFrameworkCore;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class ParticipantRepository(MonthwiseDbContext db) : IParticipantRepository
{
    public async Task<IReadOnlyList<Participant>> ListAsync(CancellationToken cancellationToken) =>
        await db.Participants.ToListAsync(cancellationToken);

    public Task<Participant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Participants.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var lower = name.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(), matching the unique index
        return db.Participants.AnyAsync(p => p.Name.ToLower() == lower && p.Id != exceptId, cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
    }

    public async Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.RecurringBills.AnyAsync(r => r.ShareParticipantId == id, cancellationToken)
        || await db.Bills.AnyAsync(b => b.ShareParticipantId == id, cancellationToken)
        || await db.Loans.AnyAsync(l => l.ShareParticipantId == id, cancellationToken)
        || await db.LoanPayments.AnyAsync(p => p.ShareParticipantId == id, cancellationToken);

    public void Add(Participant participant) => db.Participants.Add(participant);

    public void Remove(Participant participant) => db.Participants.Remove(participant);
}
