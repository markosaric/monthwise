using Monthwise.Domain.Users;

namespace Monthwise.Application.Common;

/// <summary>The signed-in user of the current request (login FR-2), or nobody.</summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user's id, or null when nobody is signed in.</summary>
    Guid? Id { get; }

    /// <summary>The name shown in the menu and in the Data and backups history (U1).</summary>
    string? DisplayName { get; }

    UserRole? Role { get; }

    bool IsHouseholdAdmin => Role == UserRole.HouseholdAdmin;
}
