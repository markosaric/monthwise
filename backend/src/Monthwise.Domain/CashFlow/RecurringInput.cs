using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>The template values of a recurring input: what each occurrence gets, and its schedule.</summary>
public sealed record RecurringInputDetails(
    Guid PayerId,
    Guid CategoryId,
    Guid AccountId,
    Money Amount,
    string? Description,
    RecurrenceSchedule Schedule);

/// <summary>
/// A regular input such as a monthly salary (FR-7), stored once. Its occurrences are calculated
/// from <see cref="Schedule"/>; an occurrence is stored as a <see cref="CashFlowInput"/> only when the
/// user changes or receives it, and a deleted occurrence is stored as a <see cref="SkippedOccurrence"/>.
/// </summary>
public sealed class RecurringInput
{
    private RecurringInput()
    {
    }

    public Guid Id { get; private set; }
    public Guid PayerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid AccountId { get; private set; }
    public Money Amount { get; private set; }
    public string? Description { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }
    public DateOnly IssueAnchor { get; private set; }
    public DateOnly ExpectedAnchor { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public RecurrenceSchedule Schedule => new(IssueAnchor, ExpectedAnchor, Frequency, EndDate);

    public RecurringInputDetails Details => new(PayerId, CategoryId, AccountId, Amount, Description, Schedule);

    public static Result<RecurringInput> Create(RecurringInputDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return Result.Failure<RecurringInput>(errors);
        }
        var recurring = new RecurringInput { Id = Guid.CreateVersion7(now), CreatedAt = now };
        recurring.Apply(details, now);
        return Result.Success(recurring);
    }

    /// <summary>
    /// Changes the template. <paramref name="lastReceivedOccurrence"/> is the latest occurrence date
    /// that was already received; a changed schedule must start after it, so no month gets the same
    /// occurrence twice.
    /// </summary>
    public Result Update(RecurringInputDetails details, DateOnly? lastReceivedOccurrence, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (ScheduleChanges(details.Schedule)
            && lastReceivedOccurrence is { } last
            && details.Schedule.ExpectedAnchor <= last)
        {
            errors.Add(CashFlowErrors.ScheduleBeforeReceived);
        }
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        return Result.Success();
    }

    /// <summary>Stops the recurring input: no occurrences after today.</summary>
    public Result Stop(DateOnly today, DateTimeOffset now)
    {
        if (IsStoppedOn(today))
        {
            return Result.Failure(CashFlowErrors.RecurringStopped);
        }
        // An input stopped before its first occurrence ends on that day's eve, so it has none.
        EndDate = today < ExpectedAnchor ? ExpectedAnchor.AddDays(-1) : today;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>True when it has no occurrences after <paramref name="today"/> any more.</summary>
    public bool IsStoppedOn(DateOnly today) => EndDate is { } end && (end <= today || end < ExpectedAnchor);

    /// <summary>The values of the calculated occurrence with the given dates.</summary>
    public InputDetails OccurrenceDetails(OccurrenceDates dates) =>
        new(PayerId, CategoryId, AccountId, Amount, dates.IssueDate, dates.ExpectedDate, null, Description);

    public bool ScheduleChanges(RecurrenceSchedule schedule) =>
        schedule.IssueAnchor != IssueAnchor || schedule.ExpectedAnchor != ExpectedAnchor || schedule.Frequency != Frequency;

    private static List<DomainError> Validate(RecurringInputDetails details)
    {
        var schedule = details.Schedule;
        var errors = CashFlowInput.Validate(new InputDetails(
            details.PayerId, details.CategoryId, details.AccountId, details.Amount,
            schedule.IssueAnchor, schedule.ExpectedAnchor, null, details.Description));
        if (!Enum.IsDefined(schedule.Frequency))
        {
            errors.Add(CashFlowErrors.FrequencyRequired);
        }
        if (schedule.EndDate is { } end && end < schedule.ExpectedAnchor)
        {
            errors.Add(CashFlowErrors.EndDateBeforeStart);
        }
        return errors;
    }

    private void Apply(RecurringInputDetails details, DateTimeOffset now)
    {
        PayerId = details.PayerId;
        CategoryId = details.CategoryId;
        AccountId = details.AccountId;
        Amount = details.Amount;
        Description = CashFlowInput.NormalizeDescription(details.Description);
        Frequency = details.Schedule.Frequency;
        IssueAnchor = details.Schedule.IssueAnchor;
        ExpectedAnchor = details.Schedule.ExpectedAnchor;
        EndDate = details.Schedule.EndDate;
        UpdatedAt = now;
    }
}

/// <summary>An occurrence of a recurring input that the user deleted, so it is not calculated again.</summary>
public sealed class SkippedOccurrence
{
    private SkippedOccurrence()
    {
    }

    public SkippedOccurrence(Guid recurringInputId, DateOnly occurrenceDate)
    {
        RecurringInputId = recurringInputId;
        OccurrenceDate = occurrenceDate;
    }

    public Guid RecurringInputId { get; private set; }
    public DateOnly OccurrenceDate { get; private set; }
}
