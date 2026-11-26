using Microsoft.EntityFrameworkCore;
using Monthwise.Application.Lendings;
using Monthwise.Domain.Lendings;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class LendingRepository(MonthwiseDbContext db) : ILendingRepository
{
    public async Task<IReadOnlyList<Lending>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<Lending>(cancellationToken);

    public Task<Lending?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Lendings.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public void Add(Lending lending) => db.Lendings.Add(lending);

    public void Remove(Lending lending) => db.Lendings.Remove(lending);
}
