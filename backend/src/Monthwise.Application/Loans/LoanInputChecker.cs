using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Application.Loans;

/// <summary>The checked request: the loan details and whether its category allows a residual value.</summary>
public sealed record CheckedLoan(LoanDetails Details, bool ResidualValueAllowed);

/// <summary>
/// Turns a loan request into <see cref="LoanDetails"/>: parses the months, calculates the number of
/// payments from the end month (A10, A11), and checks the category, subcategory, lender and account.
/// The loan rules themselves are applied by <see cref="Loan"/>. All problems are reported together.
/// </summary>
public sealed class LoanInputChecker(
    ILoanCategoryRepository categories,
    ILoanSubcategoryRepository subcategories,
    ICardListRepository<Bank> banks,
    ICardRepository cards,
    TimeProvider timeProvider)
{
    /// <param name="existing">The loan being edited; its category and subcategory stay allowed when hidden since.</param>
    public async Task<Result<CheckedLoan>> CheckAsync(LoanRequest request, Loan? existing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();

        if (!YearMonth.TryParse(request.StartMonth, out var startMonth))
        {
            errors.Add(LoanErrors.StartMonthInvalid);
        }
        var paymentCount = PaymentCount(request, startMonth, errors);

        var allowsResidual = false;
        if (request.CategoryId != Guid.Empty)
        {
            var category = await categories.GetAsync(request.CategoryId, cancellationToken);
            if (category is null)
            {
                errors.Add(LoanErrors.CategoryUnknown);
            }
            else
            {
                allowsResidual = category.AllowsResidualValue;
                if (category.IsHidden && existing?.CategoryId != category.Id)
                {
                    errors.Add(LoanErrors.CategoryHidden);
                }
            }
        }
        if (request.SubcategoryId != Guid.Empty)
        {
            var subcategory = await subcategories.GetAsync(request.SubcategoryId, cancellationToken);
            if (subcategory is null)
            {
                errors.Add(LoanErrors.SubcategoryUnknown);
            }
            else if (subcategory.CategoryId != request.CategoryId)
            {
                errors.Add(LoanErrors.SubcategoryNotInCategory);
            }
            else if (subcategory.IsHidden && existing?.SubcategoryId != subcategory.Id)
            {
                errors.Add(LoanErrors.SubcategoryHidden);
            }
        }
        if (request.LenderId != Guid.Empty && !await banks.ExistsAsync(request.LenderId, cancellationToken))
        {
            errors.Add(LoanErrors.LenderUnknown);
        }
        if (request.PaymentMethod == PaymentMethod.StandingOrder && request.AccountId is { } accountId && accountId != Guid.Empty)
        {
            var account = await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken);
            if (account is null)
            {
                errors.Add(LoanErrors.AccountUnknown);
            }
            else if (!LoanAccount.CanBeChosen(account, timeProvider.Today()))
            {
                errors.Add(LoanErrors.AccountNotPrimary);
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure<CheckedLoan>(errors);
        }
        var details = new LoanDetails(
            request.Name,
            request.CategoryId,
            request.SubcategoryId,
            request.LenderId,
            request.PaymentMethod,
            request.AccountId,
            startMonth,
            request.PaymentDay,
            Money.FromCents(request.MonthlyPaymentCents),
            paymentCount,
            request.ResidualValueCents is { } residual ? Money.FromCents(residual) : null,
            request.Description);
        return Result.Success(new CheckedLoan(details, allowsResidual));
    }

    /// <summary>The number of payments: entered, or calculated from the end month (A10, A11).</summary>
    private static int PaymentCount(LoanRequest request, YearMonth startMonth, List<DomainError> errors)
    {
        if (string.IsNullOrWhiteSpace(request.EndMonth))
        {
            if (request.PaymentCount is null)
            {
                errors.Add(LoanErrors.PaymentCountOrEndMonthRequired);
                return 0;
            }
            return request.PaymentCount.Value;
        }
        if (!YearMonth.TryParse(request.EndMonth, out var endMonth))
        {
            errors.Add(LoanErrors.EndMonthInvalid);
            return 0;
        }
        if (startMonth == default)
        {
            return 0;
        }
        if (endMonth < startMonth)
        {
            errors.Add(LoanErrors.EndMonthBeforeStart);
            return 0;
        }
        var count = Loan.PaymentCountBetween(startMonth, endMonth);
        if (request.PaymentCount is { } entered && entered != count)
        {
            errors.Add(LoanErrors.PaymentCountAndEndMonthDiffer);
        }
        return count;
    }
}
