using Monthwise.Domain.Users;

namespace Monthwise.Application.Users;

/// <summary>A user of the household as the screens show it (login U1 to U5).</summary>
public sealed record UserResponse(
    Guid Id,
    string DisplayName,
    string Username,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword);

/// <summary>The Users screen (FR-6): every user for a household admin, only oneself for a member.</summary>
public sealed record UserListResponse(IReadOnlyList<UserResponse> Users, bool CanManage);

/// <summary>A new user with the temporary password the household admin gives them (FR-6, FR-7).</summary>
public sealed record NewUserRequest(string? DisplayName, string? Username, UserRole Role, string? TemporaryPassword);

/// <summary>Changes to a user (FR-6).</summary>
public sealed record UserRequest(string? DisplayName, string? Username, UserRole Role, bool IsActive);

/// <summary>A new temporary password for another user (FR-7).</summary>
public sealed record PasswordResetRequest(string? TemporaryPassword);

/// <summary>A user changing their own password (FR-7, FR-8).</summary>
public sealed record PasswordChangeRequest(string? CurrentPassword, string? NewPassword);

/// <summary>Signing in (FR-2, FR-4).</summary>
public sealed record SignInRequest(string? Username, string? Password, bool StaySignedIn);

/// <summary>The first household admin, or a recovery with a new setup code (FR-1, FR-9).</summary>
public sealed record SetupRequest(string? SetupCode, string? DisplayName, string? Username, string? Password);

/// <summary>Whether setup is needed and who is signed in, if anybody (FR-1, FR-2, FR-7).</summary>
public sealed record AuthStateResponse(bool SetupNeeded, UserResponse? User);

/// <summary>A user account as stored, without anything secret.</summary>
public sealed record UserAccount(
    Guid Id,
    string DisplayName,
    string Username,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword)
{
    public UserResponse ToResponse() => new(Id, DisplayName, Username, Role, IsActive, MustChangePassword);

    public UserSnapshot ToSnapshot() => new(Id, Role, IsActive);
}
