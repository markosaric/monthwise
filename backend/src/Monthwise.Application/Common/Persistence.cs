namespace Monthwise.Application.Common;

/// <summary>Saves all changes made through the repositories of one request in one transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="DatabaseRuleException">
    /// The database refused the change because of one of its rules, usually after a concurrent change.
    /// Nothing of the request is saved.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>The kind of database rule that refused a change.</summary>
public enum DatabaseRule
{
    /// <summary>A value that must be unique is already in use, e.g. two requests adding the same card name at once.</summary>
    Unique,

    /// <summary>The change refers to a record that does not exist (any more).</summary>
    MissingReference,

    /// <summary>A delete was refused because other records still refer to the record.</summary>
    StillReferenced,

    /// <summary>A value or row is not allowed: a required value is missing, or a value or combination is out of range.</summary>
    ValueNotAllowed,
}

/// <summary>
/// Thrown when the database refuses a change because of one of its rules. Handlers check the rules
/// first; this covers races and anything the checks miss. The message never holds the values of the
/// refused record (error mapping FR-5).
/// </summary>
public sealed class DatabaseRuleException(DatabaseRule rule, Exception? innerException = null)
    : Exception(MessageOf(rule), innerException)
{
    public DatabaseRule Rule { get; } = rule;

    /// <summary>The message the user sees for each rule (error mapping FR-1 to FR-4).</summary>
    public static string MessageOf(DatabaseRule rule) => rule switch
    {
        DatabaseRule.Unique => "A value that must be unique is already in use.",
        DatabaseRule.MissingReference => "A record this change refers to no longer exists. Reload and try again.",
        DatabaseRule.StillReferenced => "This record is still used by other data and cannot be deleted.",
        DatabaseRule.ValueNotAllowed => "Some of the values are not allowed. Check the entries and try again.",
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };
}
