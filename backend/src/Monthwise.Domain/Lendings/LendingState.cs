namespace Monthwise.Domain.Lendings;

/// <summary>The state of a lending (money lending FR-1, FR-4, FR-5). Calculated, never stored.</summary>
public enum LendingState
{
    /// <summary>Something is outstanding and the expected return date has not passed.</summary>
    Open,

    /// <summary>Something is outstanding and the expected return date is before today.</summary>
    Overdue,

    /// <summary>Nothing is outstanding.</summary>
    Returned,
}
