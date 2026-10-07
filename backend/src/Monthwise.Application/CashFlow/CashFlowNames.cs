using Monthwise.Application.Cards;
using Monthwise.Domain.CashFlow;

namespace Monthwise.Application.CashFlow;

/// <summary>Names of payers, categories and accounts, to turn entries into responses.</summary>
public sealed class CashFlowNames
{
    private readonly Dictionary<Guid, string> _payers;
    private readonly Dictionary<Guid, string> _categories;
    private readonly Dictionary<Guid, string> _accounts;

    private CashFlowNames(Dictionary<Guid, string> payers, Dictionary<Guid, string> categories, Dictionary<Guid, string> accounts)
    {
        _payers = payers;
        _categories = categories;
        _accounts = accounts;
    }

    public static async Task<CashFlowNames> LoadAsync(
        IPayerRepository payers,
        IInputCategoryRepository categories,
        ICardRepository cards,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payers);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(cards);
        return new CashFlowNames(
            (await payers.ListAsync(cancellationToken)).ToDictionary(p => p.Id, p => p.Name),
            (await categories.ListAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.Name),
            (await cards.ListAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.Name));
    }

    public NamedReference Payer(Guid id) => Reference(_payers, id);
    public NamedReference Category(Guid id) => Reference(_categories, id);
    public NamedReference Account(Guid id) => Reference(_accounts, id);

    public InputEntryResponse ToResponse(InputEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var details = entry.Details;
        return new InputEntryResponse(
            entry.Id,
            entry.Occurrence?.RecurringInputId,
            entry.Occurrence?.OccurrenceDate,
            Payer(details.PayerId),
            Category(details.CategoryId),
            Account(details.AccountId),
            details.Amount.Cents,
            details.IssueDate,
            details.ExpectedDate,
            details.ReceivedDate,
            details.Description,
            entry.IsReceived,
            entry.Occurrence is not null,
            entry.Frequency);
    }

    public RecurringInputResponse ToResponse(RecurringInput recurringInput, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(recurringInput);
        return new RecurringInputResponse(
            recurringInput.Id,
            Payer(recurringInput.PayerId),
            Category(recurringInput.CategoryId),
            Account(recurringInput.AccountId),
            recurringInput.Amount.Cents,
            recurringInput.Description,
            recurringInput.Frequency,
            recurringInput.IssueAnchor,
            recurringInput.ExpectedAnchor,
            recurringInput.EndDate,
            recurringInput.IsStoppedOn(today));
    }

    /// <summary>Entries in list order: by month date (received, or expected while not received), then payer.</summary>
    public IReadOnlyList<InputEntryResponse> ToSortedResponses(IEnumerable<InputEntry> entries) =>
        entries
            .Select(ToResponse)
            .OrderBy(e => e.ReceivedDate ?? e.ExpectedDate)
            .ThenBy(e => e.Payer.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static NamedReference Reference(Dictionary<Guid, string> names, Guid id) =>
        new(id, names.TryGetValue(id, out var name) ? name : "Unknown");
}
