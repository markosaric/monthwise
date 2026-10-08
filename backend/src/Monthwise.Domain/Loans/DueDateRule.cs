using Monthwise.Domain.Common;

namespace Monthwise.Domain.Loans;

/// <summary>
/// The due date of a payment (A8, FR-4). The payment day is clamped to the last day of a shorter
/// month. A bill due on a Saturday or Sunday moves to the following Monday, unless that Monday is in
/// the next month; then it stays on its day. A standing order keeps its day. So a payment is always
/// due in its own month.
/// </summary>
public static class DueDateRule
{
    public const int MinPaymentDay = 1;
    public const int MaxPaymentDay = 31;

    public static DateOnly DueDate(YearMonth month, int paymentDay, PaymentMethod method)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(paymentDay, MinPaymentDay);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(paymentDay, MaxPaymentDay);
        var day = Math.Min(paymentDay, month.LastDay.Day);
        var date = new DateOnly(month.Year, month.Month, day);
        if (method == PaymentMethod.StandingOrder)
        {
            return date;
        }
        var monday = date.DayOfWeek switch
        {
            DayOfWeek.Saturday => date.AddDays(2),
            DayOfWeek.Sunday => date.AddDays(1),
            _ => date,
        };
        return YearMonth.FromDate(monday) == month ? monday : date;
    }

    /// <summary>The bill date of a payment: the first day of its month.</summary>
    public static DateOnly BillDate(YearMonth month) => month.FirstDay;
}
