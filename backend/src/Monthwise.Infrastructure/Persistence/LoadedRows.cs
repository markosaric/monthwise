using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Monthwise.Infrastructure.Persistence;

/// <summary>
/// The whole tables one request has read, so every calculation of the request reuses them instead
/// of reading them again (repeated loading FR-2). One per <see cref="MonthwiseDbContext"/>, and so
/// one per request (FR-4); dropped after every save and restore (FR-3).
/// </summary>
internal sealed class LoadedRows(MonthwiseDbContext db)
{
    private readonly Dictionary<Type, object> _tables = [];

    /// <summary>All rows of the table, read from the database the first time in this request.</summary>
    /// <remarks>
    /// The rows are tracked, as a new read would return them: the same objects, with the changes this
    /// request has made but not saved yet, and without the rows it added but not saved yet.
    /// </remarks>
    public async Task<IReadOnlyList<T>> AllAsync<T>(CancellationToken cancellationToken)
        where T : class
    {
        if (_tables.TryGetValue(typeof(T), out var rows))
        {
            return (IReadOnlyList<T>)rows;
        }
        var list = (await db.Set<T>().ToListAsync(cancellationToken)).AsReadOnly();
        _tables[typeof(T)] = list;
        return list;
    }

    /// <summary>
    /// The rows that match <paramref name="filter"/>, in the order of <see cref="AllAsync{T}"/>. The
    /// filter must give the same result in memory as in SQL. While this request has unsaved changes
    /// to rows of the table, the filter runs in the database, so it compares the stored values as
    /// it does without this store.
    /// </summary>
    public async Task<IReadOnlyList<T>> WhereAsync<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (HasUnsavedChanges<T>())
        {
            return await db.Set<T>().Where(filter).ToListAsync(cancellationToken);
        }
        var matches = filter.Compile();
        return (await AllAsync<T>(cancellationToken)).Where(matches).ToList().AsReadOnly();
    }

    /// <summary>Forgets every table, so the next use reads it from the database again.</summary>
    public void Clear() => _tables.Clear();

    private bool HasUnsavedChanges<T>()
        where T : class =>
        db.ChangeTracker.Entries<T>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
}
