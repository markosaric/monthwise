namespace Monthwise.Domain.CashFlow;

/// <summary>The issue and expected dates of one occurrence of a recurring input.</summary>
public readonly record struct OccurrenceDates(DateOnly IssueDate, DateOnly ExpectedDate)
{
    /// <summary>An occurrence is identified by its scheduled expected date.</summary>
    public DateOnly OccurrenceDate => ExpectedDate;
}

/// <summary>
/// When the occurrences of a recurring input fall. Occurrence <c>n</c> (from 0) is the anchors plus
/// <c>n</c> steps. Months are always added to the anchor, not to the previous occurrence, and clamped
/// to the end of the month, so an anchor on the 31st gives 28/29 February and then 31 March again.
/// </summary>
public sealed record RecurrenceSchedule(
    DateOnly IssueAnchor,
    DateOnly ExpectedAnchor,
    RecurrenceFrequency Frequency,
    DateOnly? EndDate)
{
    /// <summary>Occurrence number <paramref name="n"/>, counted from 0.</summary>
    public OccurrenceDates Occurrence(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return new OccurrenceDates(Shift(IssueAnchor, n), Shift(ExpectedAnchor, n));
    }

    /// <summary>Occurrences whose expected date is between <paramref name="from"/> and <paramref name="to"/> (inclusive), up to the end date.</summary>
    public IEnumerable<OccurrenceDates> OccurrencesBetween(DateOnly from, DateOnly to)
    {
        var last = EndDate is { } end && end < to ? end : to;
        for (var n = FirstCandidate(from); ; n++)
        {
            var occurrence = Occurrence(n);
            if (occurrence.ExpectedDate > last)
            {
                yield break;
            }
            if (occurrence.ExpectedDate >= from)
            {
                yield return occurrence;
            }
        }
    }

    /// <summary>The occurrence scheduled on <paramref name="occurrenceDate"/>, if there is one.</summary>
    public OccurrenceDates? OccurrenceOn(DateOnly occurrenceDate) =>
        OccurrencesBetween(occurrenceDate, occurrenceDate).Cast<OccurrenceDates?>().FirstOrDefault();

    private DateOnly Shift(DateOnly anchor, int n) => Frequency switch
    {
        RecurrenceFrequency.Weekly => anchor.AddDays(checked(7 * n)),
        RecurrenceFrequency.Monthly => anchor.AddMonths(n),
        RecurrenceFrequency.Quarterly => anchor.AddMonths(checked(3 * n)),
        RecurrenceFrequency.Yearly => anchor.AddMonths(checked(12 * n)),
        _ => throw new InvalidOperationException($"Unknown frequency {Frequency}."),
    };

    /// <summary>A step number at or just before the first occurrence on or after <paramref name="from"/>, so iteration does not start at 0 for far-away months.</summary>
    private int FirstCandidate(DateOnly from)
    {
        if (from <= ExpectedAnchor)
        {
            return 0;
        }
        var days = from.DayNumber - ExpectedAnchor.DayNumber;
        var months = (from.Year - ExpectedAnchor.Year) * 12 + (from.Month - ExpectedAnchor.Month);
        var steps = Frequency switch
        {
            RecurrenceFrequency.Weekly => days / 7,
            RecurrenceFrequency.Monthly => months,
            RecurrenceFrequency.Quarterly => months / 3,
            _ => months / 12,
        };
        return Math.Max(0, steps - 1);
    }
}
