using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>Builds entry responses after a change; shared by the input handlers.</summary>
public sealed class InputResponder(
    IPayerRepository payers,
    IInputCategoryRepository categories,
    ICardRepository cards,
    IRecurringInputRepository recurringInputs)
{
    public async Task<InputEntryResponse> RespondAsync(CashFlowInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        RecurrenceFrequency? frequency = input.RecurringInputId is { } id
            ? (await recurringInputs.GetAsync(id, cancellationToken))?.Frequency
            : null;
        return await RespondAsync(InputLedger.FromStored(input, frequency), cancellationToken);
    }

    public async Task<InputEntryResponse> RespondAsync(InputEntry entry, CancellationToken cancellationToken)
    {
        var names = await CashFlowNames.LoadAsync(payers, categories, cards, cancellationToken);
        return names.ToResponse(entry);
    }
}

/// <summary>Inputs of one month with totals (FR-1).</summary>
public sealed class GetMonthInputsHandler(
    InputLedger ledger,
    IPayerRepository payers,
    IInputCategoryRepository categories,
    ICardRepository cards)
{
    public async Task<MonthInputsResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var entries = await ledger.EntriesAsync(month.FirstDay, month.LastDay, cancellationToken);
        var names = await CashFlowNames.LoadAsync(payers, categories, cards, cancellationToken);
        return new MonthInputsResponse(month.ToString(), names.ToSortedResponses(entries), InputLedger.Totals(entries));
    }
}

public sealed class GetInputHandler(ICashFlowInputRepository inputs, InputResponder responder)
{
    public async Task<Result<InputEntryResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var input = await inputs.GetAsync(id, cancellationToken);
        return input is null
            ? Result.Failure<InputEntryResponse>(CashFlowErrors.InputNotFound)
            : Result.Success(await responder.RespondAsync(input, cancellationToken));
    }
}

/// <summary>Adds an input (FR-2); with a recurrence it creates a recurring input instead (FR-7).</summary>
public sealed class CreateInputHandler(
    ICashFlowInputRepository inputs,
    IRecurringInputRepository recurringInputs,
    InputDetailsResolver resolver,
    InputResponder responder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<InputEntryResponse>> HandleAsync(InputRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await resolver.ResolveAsync(InputFields.From(request), existing: null, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(resolved.Errors);
        }
        var details = resolved.Value;
        var now = timeProvider.GetUtcNow();

        if (request.Recurrence is not { } recurrence)
        {
            var input = CashFlowInput.Create(details, now);
            if (input.IsFailure)
            {
                return Result.Failure<InputEntryResponse>(input.Errors);
            }
            inputs.Add(input.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(await responder.RespondAsync(input.Value, cancellationToken));
        }

        var schedule = new RecurrenceSchedule(details.IssueDate, details.ExpectedDate, recurrence.Frequency, recurrence.EndDate);
        var template = RecurringInput.Create(
            new RecurringInputDetails(details.PayerId, details.CategoryId, details.AccountId, details.Amount, details.Description, schedule),
            now);
        if (template.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(template.Errors);
        }
        recurringInputs.Add(template.Value);

        // The first occurrence is stored only when it is already received.
        var first = InputLedger.FromOccurrence(template.Value, schedule.Occurrence(0));
        if (details.ReceivedDate is not null)
        {
            var stored = CashFlowInput.CreateOccurrence(details, template.Value.Id, details.ExpectedDate, now).Value;
            inputs.Add(stored);
            first = InputLedger.FromStored(stored, recurrence.Frequency);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await responder.RespondAsync(first, cancellationToken));
    }
}

/// <summary>Edits a stored input (FR-8).</summary>
public sealed class UpdateInputHandler(
    ICashFlowInputRepository inputs,
    InputDetailsResolver resolver,
    InputResponder responder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<InputEntryResponse>> HandleAsync(Guid id, InputRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = await inputs.GetAsync(id, cancellationToken);
        if (input is null)
        {
            return Result.Failure<InputEntryResponse>(CashFlowErrors.InputNotFound);
        }
        var resolved = await resolver.ResolveAsync(InputFields.From(request), input.Details, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(resolved.Errors);
        }
        var updated = input.Update(resolved.Value, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<InputEntryResponse>(updated.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await responder.RespondAsync(input, cancellationToken));
    }
}

/// <summary>Deletes a stored input; a stored occurrence is also skipped, so it is not calculated again (FR-8).</summary>
public sealed class DeleteInputHandler(
    ICashFlowInputRepository inputs,
    IRecurringInputRepository recurringInputs,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var input = await inputs.GetAsync(id, cancellationToken);
        if (input is null)
        {
            return Result.Failure(CashFlowErrors.InputNotFound);
        }
        if (input.RecurringInputId is { } recurringId && input.OccurrenceDate is { } date)
        {
            recurringInputs.AddSkip(new SkippedOccurrence(recurringId, date));
        }
        inputs.Remove(input);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Ticks or unticks Received on a stored input (FR-3).</summary>
public sealed class SetInputReceivedHandler(
    ICashFlowInputRepository inputs,
    InputResponder responder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<InputEntryResponse>> HandleAsync(Guid id, bool received, CancellationToken cancellationToken)
    {
        var input = await inputs.GetAsync(id, cancellationToken);
        if (input is null)
        {
            return Result.Failure<InputEntryResponse>(CashFlowErrors.InputNotFound);
        }
        if (received)
        {
            var marked = input.MarkReceived(timeProvider.Today(), timeProvider.GetUtcNow());
            if (marked.IsFailure)
            {
                return Result.Failure<InputEntryResponse>(marked.Errors);
            }
        }
        else
        {
            input.MarkExpected(timeProvider.GetUtcNow());
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await responder.RespondAsync(input, cancellationToken));
    }
}
