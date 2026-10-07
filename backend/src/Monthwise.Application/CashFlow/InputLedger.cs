using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>A stored input or a calculated occurrence of a recurring input.</summary>
public sealed record InputEntry(Guid? Id, OccurrenceKey? Occurrence, InputDetails Details, RecurrenceFrequency? Frequency)
{
    public DateOnly MonthDate => Details.ReceivedDate ?? Details.ExpectedDate;
    public bool IsReceived => Details.ReceivedDate is not null;
}

/// <summary>
/// Combines stored inputs with the calculated occurrences of recurring inputs, leaving out
/// occurrences that are stored (changed or received) or skipped (deleted).
/// </summary>
public sealed class InputLedger(ICashFlowInputRepository inputs, IRecurringInputRepository recurringInputs)
{
    /// <summary>Entries whose month date is between the dates, inclusive.</summary>
    public async Task<IReadOnlyList<InputEntry>> EntriesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var stored = await inputs.ListByMonthDateAsync(from, to, cancellationToken);
        var calculated = await CalculatedAsync(from, to, cancellationToken);
        return [.. stored.Select(Stored(await FrequenciesAsync(cancellationToken))), .. calculated];
    }

    /// <summary>Entries not received yet with an expected date on or before <paramref name="to"/> (for the expected balance).</summary>
    public async Task<IReadOnlyList<InputEntry>> ExpectedUpToAsync(DateOnly to, CancellationToken cancellationToken)
    {
        var stored = await inputs.ListExpectedUpToAsync(to, cancellationToken);
        var calculated = await CalculatedAsync(DateOnly.MinValue, to, cancellationToken);
        return [.. stored.Select(Stored(await FrequenciesAsync(cancellationToken))), .. calculated];
    }

    /// <summary>One entry from a stored input.</summary>
    public static InputEntry FromStored(CashFlowInput input, RecurrenceFrequency? frequency)
    {
        ArgumentNullException.ThrowIfNull(input);
        OccurrenceKey? key = input.RecurringInputId is { } recurringId && input.OccurrenceDate is { } date
            ? new OccurrenceKey(recurringId, date)
            : null;
        return new InputEntry(input.Id, key, input.Details, frequency);
    }

    /// <summary>One entry from a calculated occurrence.</summary>
    public static InputEntry FromOccurrence(RecurringInput recurringInput, OccurrenceDates dates)
    {
        ArgumentNullException.ThrowIfNull(recurringInput);
        return new InputEntry(
            null,
            new OccurrenceKey(recurringInput.Id, dates.OccurrenceDate),
            recurringInput.OccurrenceDetails(dates),
            recurringInput.Frequency);
    }

    private async Task<List<InputEntry>> CalculatedAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var templates = await recurringInputs.ListAsync(cancellationToken);
        if (templates.Count == 0)
        {
            return [];
        }
        var storedKeys = await inputs.ListOccurrenceKeysAsync(cancellationToken);
        var skipped = await recurringInputs.ListSkippedAsync(cancellationToken);
        var entries = new List<InputEntry>();
        foreach (var template in templates)
        {
            foreach (var dates in template.Schedule.OccurrencesBetween(from, to))
            {
                var key = new OccurrenceKey(template.Id, dates.OccurrenceDate);
                if (!storedKeys.Contains(key) && !skipped.Contains(key))
                {
                    entries.Add(FromOccurrence(template, dates));
                }
            }
        }
        return entries;
    }

    private async Task<Dictionary<Guid, RecurrenceFrequency>> FrequenciesAsync(CancellationToken cancellationToken) =>
        (await recurringInputs.ListAsync(cancellationToken)).ToDictionary(r => r.Id, r => r.Frequency);

    private static Func<CashFlowInput, InputEntry> Stored(Dictionary<Guid, RecurrenceFrequency> frequencies) =>
        input => FromStored(
            input,
            input.RecurringInputId is { } id && frequencies.TryGetValue(id, out var frequency) ? frequency : null);

    /// <summary>Expected, received and all inputs of the given entries.</summary>
    public static InputTotalsResponse Totals(IEnumerable<InputEntry> entries)
    {
        var list = entries.ToList();
        var expected = Money.Sum(list.Where(e => !e.IsReceived).Select(e => e.Details.Amount));
        var received = Money.Sum(list.Where(e => e.IsReceived).Select(e => e.Details.Amount));
        return new InputTotalsResponse(expected.Cents, received.Cents, (expected + received).Cents);
    }
}
