using Monthwise.Domain.Common;
using Monthwise.Domain.Users;

namespace Monthwise.Application.Users;

/// <summary>
/// The stored user accounts and their passwords (login FR-6 to FR-9). Passwords are given here only to
/// be hashed or checked; they are never returned, stored readable or logged. Every change is saved at once.
/// </summary>
public interface IUserAccounts
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The user with this username, compared without regard to case (U2).</summary>
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>Adds a user; fails with <see cref="UserErrors.UsernameTaken"/> when the username is used.</summary>
    Task<Result<UserAccount>> CreateAsync(
        string displayName, string username, UserRole role, string password, bool mustChangePassword, CancellationToken cancellationToken);

    /// <summary>
    /// Changes a user. Making them inactive ends their sessions; a role or name change reaches their open
    /// sessions on their next request.
    /// </summary>
    Task<Result<UserAccount>> UpdateAsync(
        Guid id, string displayName, string username, UserRole role, bool isActive, CancellationToken cancellationToken);

    /// <summary>Deletes a user; their sessions end on their next request.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password without the current one (a temporary password, FR-7, or setup, FR-9), lifts a
    /// lockout and ends all of the user's sessions.
    /// </summary>
    Task SetPasswordAsync(Guid id, string password, bool mustChangePassword, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the user's own password after checking the current one (FR-8) and clears a temporary password.
    /// The user's other sessions end; the caller renews the current one.
    /// </summary>
    Task<Result> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

/// <summary>The setup code of the installation (login FR-1, FR-9). The code itself is never stored or logged.</summary>
public interface ISetupCodes
{
    /// <summary>Whether the installation has a setup code at all.</summary>
    bool IsConfigured { get; }

    /// <summary>Whether <paramref name="code"/> is the installation's code and was not used yet.</summary>
    Task<bool> IsUsableAsync(string? code, CancellationToken cancellationToken);

    /// <summary>Remembers the code as used, so it stops working. Fails when it was used meanwhile.</summary>
    Task<bool> MarkUsedAsync(string code, CancellationToken cancellationToken);
}
