using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>The attributes the user enters for an input (requirements section 3), with the payer already resolved.</summary>
public sealed record InputDetails(
    Guid PayerId,
    Guid CategoryId,
    Guid AccountId,
    Money Amount,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    DateOnly? ReceivedDate,
    string? Description);
