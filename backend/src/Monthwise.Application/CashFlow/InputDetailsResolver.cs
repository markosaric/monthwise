using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>What the user entered for an input or the template of a recurring input.</summary>
public sealed record InputFields(
    string? PayerName,
    Guid AccountId,
    Guid CategoryId,
    long AmountCents,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    DateOnly? ReceivedDate,
    string? Description)
{
    public static InputFields From(InputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.PayerName, request.AccountId, request.CategoryId, request.AmountCents,
            request.IssueDate, request.ExpectedDate, request.ReceivedDate, request.Description);
    }

    public static InputFields From(RecurringInputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.PayerName, request.AccountId, request.CategoryId, request.AmountCents,
            request.IssueDate, request.ExpectedDate, null, request.Description);
    }
}

/// <summary>
/// Checks the account and category of an input, finds or creates its payer (FR-5), and applies the
/// input rules. All problems are reported together.
/// </summary>
public sealed class InputDetailsResolver(
    IPayerRepository payers,
    IInputCategoryRepository categories,
    ICardRepository cards,
    TimeProvider timeProvider)
{
    /// <param name="existing">The values being changed; its account and category stay allowed even when blocked or hidden since.</param>
    public async Task<Result<InputDetails>> ResolveAsync(
        InputFields fields, InputDetails? existing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new List<DomainError>();

        var payerName = Payer.ValidateName(fields.PayerName, CashFlowErrors.PayerRequired, CashFlowErrors.PayerTooLong);
        errors.AddRange(payerName.Errors);

        if (fields.CategoryId != Guid.Empty)
        {
            var category = await categories.GetAsync(fields.CategoryId, cancellationToken);
            if (category is null)
            {
                errors.Add(CashFlowErrors.CategoryUnknown);
            }
            else if (category.IsHidden && category.Id != existing?.CategoryId)
            {
                errors.Add(CashFlowErrors.CategoryHidden);
            }
        }

        if (fields.AccountId != Guid.Empty)
        {
            var account = await cards.GetAsync(fields.AccountId, includeSensitiveData: false, cancellationToken);
            if (account is null)
            {
                errors.Add(CashFlowErrors.AccountUnknown);
            }
            else if (account.Id != existing?.AccountId && !CanReceive(account, timeProvider.Today()))
            {
                errors.Add(CashFlowErrors.AccountUnusable);
            }
        }

        // The payer is created only once everything else is valid; until then a placeholder id
        // lets the input rules run.
        var details = new InputDetails(
            payerName.IsSuccess ? Guid.CreateVersion7() : Guid.Empty,
            fields.CategoryId,
            fields.AccountId,
            Money.FromCents(fields.AmountCents),
            fields.IssueDate,
            fields.ExpectedDate,
            fields.ReceivedDate,
            fields.Description);
        errors.AddRange(CashFlowInput.Validate(details).Where(e => e != CashFlowErrors.PayerRequired));

        if (errors.Count > 0)
        {
            return Result.Failure<InputDetails>(errors.Distinct());
        }

        var payer = await FindOrCreatePayerAsync(payerName.Value, cancellationToken);
        return Result.Success(details with { PayerId = payer.Id });
    }

    /// <summary>Any account that is not blocked, expired or archived can receive money (FR-4).</summary>
    public static bool CanReceive(Card account, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);
        return account.StatusOn(today) is not (CardStatus.Blocked or CardStatus.Expired or CardStatus.Archived);
    }

    private async Task<Payer> FindOrCreatePayerAsync(string name, CancellationToken cancellationToken)
    {
        var payer = await payers.FindByNameAsync(name, cancellationToken);
        if (payer is not null)
        {
            payer.Restore();
            return payer;
        }
        payer = Payer.Create(name, timeProvider.GetUtcNow()).Value;
        payers.Add(payer);
        return payer;
    }
}
