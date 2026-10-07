using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>An occurrence the user acts on: either already stored, or still calculated.</summary>
public sealed record OccurrenceTarget(RecurringInput RecurringInput, OccurrenceKey Key, CashFlowInput? Stored, OccurrenceDates Dates);

/// <summary>Finds an occurrence of a recurring input by its scheduled date.</summary>
public sealed class OccurrenceFinder(ICashFlowInputRepository inputs, IRecurringInputRepository recurringInputs)
{
    public async Task<Result<OccurrenceTarget>> FindAsync(Guid recurringInputId, DateOnly occurrenceDate, CancellationToken cancellationToken)
    {
        var recurringInput = await recurringInputs.GetAsync(recurringInputId, cancellationToken);
        if (recurringInput is null)
        {
            return Result.Failure<OccurrenceTarget>(CashFlowErrors.RecurringNotFound);
        }
        var key = new OccurrenceKey(recurringInputId, occurrenceDate);
        var stored = await inputs.GetOccurrenceAsync(key, cancellationToken);
        if (stored is not null)
        {
            return Result.Success(new OccurrenceTarget(recurringInput, key, stored, new OccurrenceDates(stored.IssueDate, stored.ExpectedDate)));
        }
        var skipped = await recurringInputs.ListSkippedAsync(cancellationToken);
        if (skipped.Contains(key) || recurringInput.Schedule.OccurrenceOn(occurrenceDate) is not { } dates)
        {
            return Result.Failure<OccurrenceTarget>(CashFlowErrors.OccurrenceNotFound);
        }
        return Result.Success(new OccurrenceTarget(recurringInput, key, null, dates));
    }
}

/// <summary>Changes one occurrence without changing the others (FR-7); the occurrence is then stored.</summary>
public sealed class UpdateOccurrenceHandler(
    OccurrenceFinder finder,
    ICashFlowInputRepository inputs,
    InputDetailsResolver resolver,
    InputResponder responder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<InputEntryResponse>> HandleAsync(
        Guid recurringInputId, DateOnly occurrenceDate, InputRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = await finder.FindAsync(recurringInputId, occurrenceDate, cancellationToken);
        if (target.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(target.Errors);
        }
        var (recurringInput, key, stored, dates) = target.Value;
        var existing = stored?.Details ?? recurringInput.OccurrenceDetails(dates);
        var resolved = await resolver.ResolveAsync(InputFields.From(request), existing, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(resolved.Errors);
        }

        var now = timeProvider.GetUtcNow();
        if (stored is null)
        {
            var created = CashFlowInput.CreateOccurrence(resolved.Value, key.RecurringInputId, key.OccurrenceDate, now);
            if (created.IsFailure)
            {
                return Result.Failure<InputEntryResponse>(created.Errors);
            }
            stored = created.Value;
            inputs.Add(stored);
        }
        else
        {
            var updated = stored.Update(resolved.Value, now);
            if (updated.IsFailure)
            {
                return Result.Failure<InputEntryResponse>(updated.Errors);
            }
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await responder.RespondAsync(stored, cancellationToken));
    }
}

/// <summary>Ticks Received on an occurrence (FR-3); a calculated occurrence is stored as received.</summary>
public sealed class ReceiveOccurrenceHandler(
    OccurrenceFinder finder,
    ICashFlowInputRepository inputs,
    InputResponder responder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<InputEntryResponse>> HandleAsync(
        Guid recurringInputId, DateOnly occurrenceDate, CancellationToken cancellationToken)
    {
        var target = await finder.FindAsync(recurringInputId, occurrenceDate, cancellationToken);
        if (target.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(target.Errors);
        }
        var (recurringInput, key, stored, dates) = target.Value;
        var now = timeProvider.GetUtcNow();
        if (stored is null)
        {
            stored = CashFlowInput.CreateOccurrence(recurringInput.OccurrenceDetails(dates), key.RecurringInputId, key.OccurrenceDate, now).Value;
            inputs.Add(stored);
        }
        var marked = stored.MarkReceived(timeProvider.Today(), now);
        if (marked.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(marked.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await responder.RespondAsync(stored, cancellationToken));
    }
}

/// <summary>Deletes one occurrence (FR-8): it is skipped, and removed if it was stored.</summary>
public sealed class DeleteOccurrenceHandler(
    OccurrenceFinder finder,
    ICashFlowInputRepository inputs,
    IRecurringInputRepository recurringInputs,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid recurringInputId, DateOnly occurrenceDate, CancellationToken cancellationToken)
    {
        var target = await finder.FindAsync(recurringInputId, occurrenceDate, cancellationToken);
        if (target.IsFailure)
        {
            return target;
        }
        if (target.Value.Stored is { } stored)
        {
            inputs.Remove(stored);
        }
        recurringInputs.AddSkip(new SkippedOccurrence(recurringInputId, occurrenceDate));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetRecurringInputHandler(
    IRecurringInputRepository recurringInputs,
    IPayerRepository payers,
    IInputCategoryRepository categories,
    ICardRepository cards,
    TimeProvider timeProvider)
{
    public async Task<Result<RecurringInputResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var recurringInput = await recurringInputs.GetAsync(id, cancellationToken);
        if (recurringInput is null)
        {
            return Result.Failure<RecurringInputResponse>(CashFlowErrors.RecurringNotFound);
        }
        var names = await CashFlowNames.LoadAsync(payers, categories, cards, cancellationToken);
        return Result.Success(names.ToResponse(recurringInput, timeProvider.Today()));
    }
}

/// <summary>
/// Changes a recurring input (FR-7). Calculated occurrences follow the template automatically;
/// stored occurrences that are not received and fall on or after today are dropped, so they are
/// calculated again from the new template. Received occurrences never change.
/// </summary>
public sealed class UpdateRecurringInputHandler(
    IRecurringInputRepository recurringInputs,
    ICashFlowInputRepository inputs,
    InputDetailsResolver resolver,
    GetRecurringInputHandler getRecurringInput,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<RecurringInputResponse>> HandleAsync(
        Guid id, RecurringInputRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recurringInput = await recurringInputs.GetAsync(id, cancellationToken);
        if (recurringInput is null)
        {
            return Result.Failure<RecurringInputResponse>(CashFlowErrors.RecurringNotFound);
        }
        var current = recurringInput.OccurrenceDetails(recurringInput.Schedule.Occurrence(0));
        var resolved = await resolver.ResolveAsync(InputFields.From(request), current, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<RecurringInputResponse>(resolved.Errors);
        }

        var details = resolved.Value;
        var occurrences = await inputs.ListOccurrencesAsync(id, cancellationToken);
        var lastReceived = occurrences.Where(o => o.IsReceived).Select(o => o.OccurrenceDate).Max();
        var updated = recurringInput.Update(
            new RecurringInputDetails(
                details.PayerId, details.CategoryId, details.AccountId, details.Amount, details.Description,
                new RecurrenceSchedule(details.IssueDate, details.ExpectedDate, request.Frequency, request.EndDate)),
            lastReceived,
            timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<RecurringInputResponse>(updated.Errors);
        }

        var today = timeProvider.Today();
        foreach (var occurrence in occurrences.Where(o => !o.IsReceived && o.OccurrenceDate >= today))
        {
            inputs.Remove(occurrence);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getRecurringInput.HandleAsync(id, cancellationToken);
    }
}

/// <summary>Stops a recurring input (FR-7): no occurrences after today; received ones stay.</summary>
public sealed class StopRecurringInputHandler(
    IRecurringInputRepository recurringInputs,
    ICashFlowInputRepository inputs,
    GetRecurringInputHandler getRecurringInput,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<RecurringInputResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var recurringInput = await recurringInputs.GetAsync(id, cancellationToken);
        if (recurringInput is null)
        {
            return Result.Failure<RecurringInputResponse>(CashFlowErrors.RecurringNotFound);
        }
        var today = timeProvider.Today();
        var stopped = recurringInput.Stop(today, timeProvider.GetUtcNow());
        if (stopped.IsFailure)
        {
            return Result.Failure<RecurringInputResponse>(stopped.Errors);
        }
        var occurrences = await inputs.ListOccurrencesAsync(id, cancellationToken);
        foreach (var occurrence in occurrences.Where(o => !o.IsReceived && o.OccurrenceDate > recurringInput.EndDate))
        {
            inputs.Remove(occurrence);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getRecurringInput.HandleAsync(id, cancellationToken);
    }
}
