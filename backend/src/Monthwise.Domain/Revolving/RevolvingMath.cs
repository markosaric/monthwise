using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Revolving;

/// <summary>
/// The amounts of a revolving card (revolving FR-3, FR-7), calculated with whole cents and basis points
/// only, never with floating point (NFR-1). Results are rounded to the cent, half away from zero.
/// </summary>
public static class RevolvingMath
{
    public const int MonthsPerYear = 12;

    /// <summary><paramref name="basisPoints"/> of <paramref name="amount"/>, divided by <paramref name="divisor"/>, rounded to the cent.</summary>
    public static Money PercentOf(Money amount, long basisPoints, long divisor = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(divisor, 1);
        var numerator = checked(amount.Cents * basisPoints);
        var denominator = checked(RevolvingTerms.FullBasisPoints * divisor);
        var quotient = Math.DivRem(numerator, denominator, out var remainder);
        if (Math.Abs(remainder) * 2 >= denominator)
        {
            quotient += Math.Sign(numerator);
        }
        return Money.FromCents(quotient);
    }

    /// <summary>
    /// The minimum payment of a debt (FR-3): the greater of R1 of the debt and R2, but never more than
    /// the whole debt. Zero without debt.
    /// </summary>
    public static Money MinimumPayment(Money debt, RevolvingTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        if (!debt.IsPositive)
        {
            return Money.Zero;
        }
        var percent = PercentOf(debt, terms.MinPaymentBasisPoints);
        var minimum = percent > terms.MinPaymentAmount ? percent : terms.MinPaymentAmount;
        return minimum > debt ? debt : minimum;
    }

    /// <summary>The interest of one month on <paramref name="debt"/> (FR-7): debt × yearly rate ÷ 12. Zero without debt.</summary>
    public static Money MonthlyInterest(Money debt, RevolvingTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        return debt.IsPositive ? PercentOf(debt, terms.InterestBasisPoints, MonthsPerYear) : Money.Zero;
    }
}
