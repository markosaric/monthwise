using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Loans;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

/// <summary>
/// Edits, ticks, unticks and resets the contribution of a shared bill or loan payment (shared bills FR-5,
/// FR-6). A contribution is stored only once it is received or edited; until then it is calculated from
/// its cost.
/// </summary>
public sealed class ContributionHandler(
    ICashFlowInputRepository inputs,
    ICardRepository cards,
    LoanLedger loanLedger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Edits the contribution; it then keeps its values until reset to the part.</summary>
    public async Task<Result<ContributionResponse>> EditAsync(
        ContributionSource source, ContributionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = await SharedAsync(cancellationToken);
        if (book.Cost(source) is not { } cost)
        {
            return Result.Failure<ContributionResponse>(SharedCostErrors.NotShared);
        }
        var stored = book.Stored(source);
        var errors = new List<DomainError>();
        if (request.AccountId == Guid.Empty)
        {
            errors.Add(SharedCostErrors.ContributionNeedsAccount);
        }
        else if (request.AccountId != stored?.AccountId && request.AccountId != cost.Contribution.AccountId)
        {
            var account = await cards.GetAsync(request.AccountId, includeSensitiveData: false, cancellationToken);
            if (account is null)
            {
                errors.Add(CashFlowErrors.AccountUnknown);
            }
            else if (PaidFromRules.IsCreditCard(account) || !InputDetailsResolver.CanReceive(account, timeProvider.Today()))
            {
                errors.Add(CashFlowErrors.AccountUnusable);
            }
        }
        var details = new InputDetails(
            cost.Contribution.PayerId,
            ContributionValues.AgreedContributionsCategoryId,
            request.AccountId,
            Money.FromCents(request.AmountCents),
            stored?.IssueDate ?? cost.Contribution.IssueDate,
            request.ExpectedDate,
            request.ReceivedDate,
            request.Description);
        errors.AddRange(CashFlowInput.Validate(details).Where(e => e != CashFlowErrors.AccountRequired));
        if (errors.Count > 0)
        {
            return Result.Failure<ContributionResponse>(errors.Distinct());
        }

        var now = timeProvider.GetUtcNow();
        if (stored is null)
        {
            inputs.Add(CashFlowInput.CreateContribution(details, source, isEdited: true, now).Value);
        }
        else
        {
            stored.Update(details, now);
            stored.SetEdited(true, now);
        }
        return await SaveAsync(source, cancellationToken);
    }

    /// <summary>Ticks the contribution as received: today, or the given date, to its account or the given one.</summary>
    public async Task<Result<ContributionResponse>> MarkReceivedAsync(
        ContributionSource source, ContributionReceivedRequest? request, CancellationToken cancellationToken)
    {
        var book = await SharedAsync(cancellationToken);
        if (book.Cost(source) is not { } cost)
        {
            return Result.Failure<ContributionResponse>(SharedCostErrors.NotShared);
        }
        var today = timeProvider.Today();
        var now = timeProvider.GetUtcNow();
        var receivedDate = request?.ReceivedDate ?? today;
        if (book.Stored(source) is { } stored)
        {
            var details = stored.Details with
            {
                AccountId = request?.AccountId is { } given && given != Guid.Empty ? given : stored.AccountId,
                ReceivedDate = receivedDate,
            };
            var updated = stored.Update(details, now);
            return updated.IsFailure ? Result.Failure<ContributionResponse>(updated.Errors) : await SaveAsync(source, cancellationToken);
        }

        var accountId = request?.AccountId is { } chosen && chosen != Guid.Empty ? chosen : cost.Contribution.AccountId;
        if (accountId is null)
        {
            return Result.Failure<ContributionResponse>(SharedCostErrors.ContributionNeedsAccount);
        }
        var created = CashFlowInput.CreateContribution(
            SharedCostBook.DetailsOf(cost) with { AccountId = accountId.Value, ReceivedDate = receivedDate },
            source,
            isEdited: false,
            now);
        if (created.IsFailure)
        {
            return Result.Failure<ContributionResponse>(created.Errors);
        }
        inputs.Add(created.Value);
        return await SaveAsync(source, cancellationToken);
    }

    /// <summary>Unticks Received: a contribution that was not edited follows its cost again.</summary>
    public async Task<Result<ContributionResponse>> MarkExpectedAsync(ContributionSource source, CancellationToken cancellationToken)
    {
        var book = await SharedAsync(cancellationToken);
        if (book.Cost(source) is null)
        {
            return Result.Failure<ContributionResponse>(SharedCostErrors.NotShared);
        }
        if (book.Stored(source) is { } stored)
        {
            if (stored.IsEdited)
            {
                stored.MarkExpected(timeProvider.GetUtcNow());
            }
            else
            {
                inputs.Remove(stored);
            }
        }
        return await SaveAsync(source, cancellationToken);
    }

    /// <summary>
    /// Reset to part (FR-6): an expected contribution follows its cost again; a received one gets the
    /// current part as its amount.
    /// </summary>
    public async Task<Result<ContributionResponse>> ResetAsync(ContributionSource source, CancellationToken cancellationToken)
    {
        var book = await SharedAsync(cancellationToken);
        if (book.Cost(source) is not { } cost)
        {
            return Result.Failure<ContributionResponse>(SharedCostErrors.NotShared);
        }
        if (book.Stored(source) is not { } stored)
        {
            return Result.Failure<ContributionResponse>(SharedCostErrors.ContributionNotStored);
        }
        if (stored.IsReceived)
        {
            if (!cost.Contribution.Amount.IsPositive)
            {
                return Result.Failure<ContributionResponse>(CashFlowErrors.AmountNotPositive);
            }
            stored.ResetAmount(cost.Contribution.Amount, timeProvider.GetUtcNow());
        }
        else
        {
            inputs.Remove(stored);
        }
        return await SaveAsync(source, cancellationToken);
    }

    private async Task<SharedCostBook> SharedAsync(CancellationToken cancellationToken) =>
        (await loanLedger.LoadAsync(cancellationToken)).Shared;

    private async Task<Result<ContributionResponse>> SaveAsync(ContributionSource source, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var book = await SharedAsync(cancellationToken);
        return Result.Success(book.ContributionOf(source)!);
    }
}
