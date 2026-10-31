namespace Monthwise.Application.DataTransfer;

/// <summary>
/// Lets one import run at a time and tells the API to refuse other calls while it runs (backup and
/// restore FR-7). Every target runs one instance of Monthwise, so a process-wide gate is enough.
/// </summary>
public sealed class ImportGate
{
    private int _running;
    private long _lastImportedAtTicks;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>When data was last imported in this process; open screens reload their data when it changes.</summary>
    public DateTimeOffset? LastImportedAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastImportedAtTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        internal set => Interlocked.Exchange(ref _lastImportedAtTicks, value?.UtcTicks ?? 0);
    }

    public bool TryEnter() => Interlocked.CompareExchange(ref _running, 1, 0) == 0;

    public void Exit() => Volatile.Write(ref _running, 0);
}
