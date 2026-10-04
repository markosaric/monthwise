namespace Monthwise.Application.Common;

/// <summary>Saves all changes made through the repositories of one request in one transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="UniqueConstraintException">A unique rule was broken by a concurrent change.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Thrown when the database rejects a change because of a unique index, e.g. two requests adding
/// the same card name at the same time. Handlers check uniqueness first; this covers the race.
/// </summary>
public sealed class UniqueConstraintException(string message, Exception? innerException = null)
    : Exception(message, innerException);
