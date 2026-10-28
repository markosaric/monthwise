namespace Monthwise.Infrastructure.Persistence;

/// <summary>Outcome of applying migrations on startup.</summary>
public enum MigrationStatus
{
    /// <summary>Not applied (yet): startup migration is still running or turned off.</summary>
    Pending,
    Applied,
    /// <summary>Applying failed; the database was left as it was and the API refuses to work on it.</summary>
    Failed,
}

/// <summary>Remembers whether startup migrations were applied, for the health response and the migration gate.</summary>
public sealed class DatabaseMigrationState
{
    private volatile MigrationStatus _status = MigrationStatus.Pending;

    public MigrationStatus Status
    {
        get => _status;
        internal set => _status = value;
    }
}
