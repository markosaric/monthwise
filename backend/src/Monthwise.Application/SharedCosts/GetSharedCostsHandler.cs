using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

/// <summary>
/// The Shared costs screen (FR-8): for each participant, the recurring bills and loans they share, the
/// parts and contributions of the month, and the contributions still outstanding at the end of the month.
/// Archived participants are shown while something is outstanding.
/// </summary>
public sealed class GetSharedCostsHandler(
    LoanLedger loanLedger,
    IRecurringBillRepository recurringBills,
    ParticipantHandler participantHandler)
{
    public async Task<SharedCostsResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var loans = await loanLedger.LoadAsync(cancellationToken);
        var book = loans.Shared;
        var recurring = await recurringBills.ListAsync(cancellationToken);
        var contributions = book.Contributions().ToList();
        var result = new List<ParticipantSharedCostsResponse>();
        foreach (var participant in book.Participants.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var costs = recurring
                .Where(r => r.IsActive && r.Share?.ParticipantId == participant.Id)
                .Select(r => new SharedCostResponse(ContributionSourceKind.Bill, r.Id, r.Name, book.ToResponse(r.Share)!))
                .Concat(loans.Loans
                    .Where(l => !l.IsClosed && l.Share?.ParticipantId == participant.Id)
                    .Select(l => new SharedCostResponse(ContributionSourceKind.LoanPayment, l.Id, l.Name, book.ToResponse(l.Share)!)))
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var own = contributions.Where(c => c.Participant.Id == participant.Id).ToList();
            var inMonth = own
                .Where(c => YearMonth.FromDate(c.ReceivedDate ?? c.ExpectedDate) == month)
                .OrderBy(c => c.ReceivedDate ?? c.ExpectedDate)
                .ThenBy(c => c.CostName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var outstanding = own
                .Where(c => !c.IsReceived && c.ExpectedDate <= month.LastDay && c.AmountCents > 0)
                .OrderBy(c => c.ExpectedDate)
                .ThenBy(c => c.CostName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (participant.IsArchived && costs.Count == 0 && outstanding.Count == 0 && inMonth.Count == 0)
            {
                continue;
            }
            result.Add(new ParticipantSharedCostsResponse(
                await participantHandler.ResponseAsync(participant, cancellationToken),
                costs,
                Money.Sum(inMonth.Select(c => Money.FromCents(c.PartCents))).Cents,
                Money.Sum(inMonth.Where(c => c.IsReceived).Select(c => Money.FromCents(c.AmountCents))).Cents,
                Money.Sum(outstanding.Select(c => Money.FromCents(c.AmountCents))).Cents,
                inMonth,
                outstanding));
        }
        return new SharedCostsResponse(month.ToString(), result);
    }
}
