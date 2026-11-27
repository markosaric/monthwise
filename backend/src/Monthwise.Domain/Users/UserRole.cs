namespace Monthwise.Domain.Users;

/// <summary>What a user of the household may do (login U4).</summary>
public enum UserRole
{
    /// <summary>Manages users and exports and imports data, besides everything a member does.</summary>
    HouseholdAdmin,

    /// <summary>Sees and changes household data.</summary>
    Member,
}
