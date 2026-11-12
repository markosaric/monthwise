using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Monthwise.Api.Tests;

/// <summary>
/// Collects the text of every SQL statement EF Core runs in this process while it is open
/// (repeated loading FR-6). Test classes using it share the database collection, so they never
/// run at the same time as another database test.
/// </summary>
public sealed class StatementCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly IDisposable _subscription;
    private readonly ConcurrentBag<IDisposable> _listeners = [];

    public StatementCounter() => _subscription = DiagnosticListener.AllListeners.Subscribe(this);

    public ConcurrentQueue<string> Statements { get; } = new();

    public void Dispose()
    {
        _subscription.Dispose();
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
    {
        if (value.Name == DbLoggerCategory.Name)
        {
            _listeners.Add(value.Subscribe(this));
        }
    }

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == RelationalEventId.CommandExecuting.Name && value.Value is CommandEventData data)
        {
            Statements.Enqueue(data.Command.CommandText);
        }
    }

    void IObserver<DiagnosticListener>.OnCompleted()
    {
    }

    void IObserver<DiagnosticListener>.OnError(Exception error)
    {
    }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted()
    {
    }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error)
    {
    }
}
