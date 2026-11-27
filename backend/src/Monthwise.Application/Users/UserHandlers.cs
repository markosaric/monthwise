using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.Users;

namespace Monthwise.Application.Users;

/// <summary>The Users screen (login FR-6, FR-7). Changing users is for household admins; the endpoints check that.</summary>
public sealed class UsersHandler(IUserAccounts accounts, ICurrentUser currentUser)
{
    public async Task<UserListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var users = await accounts.ListAsync(cancellationToken);
        var shown = currentUser.IsHouseholdAdmin ? users : users.Where(u => u.Id == currentUser.Id).ToList();
        return new UserListResponse(
            shown.OrderBy(u => u.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(u => u.ToResponse()).ToList(),
            currentUser.IsHouseholdAdmin);
    }

    public async Task<Result<UserResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = currentUser.IsHouseholdAdmin || id == currentUser.Id ? await accounts.FindAsync(id, cancellationToken) : null;
        return user is null ? Result.Failure<UserResponse>(UserErrors.UserNotFound) : Result.Success(user.ToResponse());
    }

    public async Task<Result<UserResponse>> CreateAsync(NewUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (displayName, nameErrors) = UserRules.ValidateDisplayName(request.DisplayName);
        var (username, usernameErrors) = UserRules.ValidateUsername(request.Username);
        var errors = nameErrors
            .Concat(usernameErrors)
            .Concat(UserRules.ValidatePassword(request.TemporaryPassword, "temporaryPassword"))
            .ToList();
        if (errors.Count > 0)
        {
            return Result.Failure<UserResponse>(errors);
        }

        var created = await accounts.CreateAsync(
            displayName!, username!, request.Role, request.TemporaryPassword!, mustChangePassword: true, cancellationToken);
        return created.IsSuccess ? Result.Success(created.Value.ToResponse()) : Result.Failure<UserResponse>(created.Errors);
    }

    public async Task<Result<UserResponse>> UpdateAsync(Guid id, UserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var users = await accounts.ListAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.UserNotFound);
        }

        var (displayName, nameErrors) = UserRules.ValidateDisplayName(request.DisplayName);
        var (username, usernameErrors) = UserRules.ValidateUsername(request.Username);
        var errors = nameErrors.Concat(usernameErrors).ToList();
        if (errors.Count > 0)
        {
            return Result.Failure<UserResponse>(errors);
        }

        var remains = UserRules.EnsureHouseholdAdminRemains(
            users.Select(u => u.ToSnapshot()).ToList(), new UserSnapshot(id, request.Role, request.IsActive), id);
        if (remains.IsFailure)
        {
            return Result.Failure<UserResponse>(remains.Errors);
        }

        var updated = await accounts.UpdateAsync(id, displayName!, username!, request.Role, request.IsActive, cancellationToken);
        return updated.IsSuccess ? Result.Success(updated.Value.ToResponse()) : Result.Failure<UserResponse>(updated.Errors);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var users = await accounts.ListAsync(cancellationToken);
        if (users.All(u => u.Id != id))
        {
            return Result.Failure(UserErrors.UserNotFound);
        }

        var remains = UserRules.EnsureHouseholdAdminRemains(users.Select(u => u.ToSnapshot()).ToList(), null, id);
        if (remains.IsFailure)
        {
            return remains;
        }

        await accounts.DeleteAsync(id, cancellationToken);
        return Result.Success();
    }

    /// <summary>A new temporary password for a user (FR-7); ends all of that user's sessions.</summary>
    public async Task<Result<UserResponse>> ResetPasswordAsync(Guid id, PasswordResetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await accounts.FindAsync(id, cancellationToken) is null)
        {
            return Result.Failure<UserResponse>(UserErrors.UserNotFound);
        }

        var errors = UserRules.ValidatePassword(request.TemporaryPassword, "temporaryPassword");
        if (errors.Count > 0)
        {
            return Result.Failure<UserResponse>(errors);
        }

        await accounts.SetPasswordAsync(id, request.TemporaryPassword!, mustChangePassword: true, cancellationToken);
        return Result.Success((await accounts.FindAsync(id, cancellationToken))!.ToResponse());
    }
}

/// <summary>A user changing their own password (FR-7, FR-8).</summary>
public sealed class ChangePasswordHandler(IUserAccounts accounts, ICurrentUser currentUser)
{
    public async Task<Result> HandleAsync(PasswordChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentUser.Id is not { } id)
        {
            return Result.Failure(UserErrors.UserNotFound);
        }

        var errors = UserRules.ValidatePassword(request.NewPassword, "newPassword");
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        return await accounts.ChangePasswordAsync(id, request.CurrentPassword ?? string.Empty, request.NewPassword!, cancellationToken);
    }
}

/// <summary>
/// The first household admin (FR-1), or with a new setup code: a new password for an existing household
/// admin or a new household admin (FR-9). Household data is never touched.
/// </summary>
public sealed class SetupHandler(IUserAccounts accounts, ISetupCodes setupCodes)
{
    public async Task<Result<UserAccount>> HandleAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!setupCodes.IsConfigured)
        {
            return Result.Failure<UserAccount>(UserErrors.SetupCodeNotConfigured);
        }
        if (!await setupCodes.IsUsableAsync(request.SetupCode, cancellationToken))
        {
            return Result.Failure<UserAccount>(UserErrors.SetupCodeWrong);
        }

        var (username, usernameErrors) = UserRules.ValidateUsername(request.Username);
        var existing = username is null ? null : await accounts.FindByUsernameAsync(username, cancellationToken);
        var (displayName, nameErrors) = existing is not null && string.IsNullOrWhiteSpace(request.DisplayName)
            ? (existing.DisplayName, [])
            : UserRules.ValidateDisplayName(request.DisplayName);
        var errors = nameErrors
            .Concat(usernameErrors)
            .Concat(UserRules.ValidatePassword(request.Password, "password"))
            .ToList();
        if (existing is { Role: not UserRole.HouseholdAdmin })
        {
            errors.Add(UserErrors.SetupUsernameNotAdmin);
        }
        if (errors.Count > 0)
        {
            return Result.Failure<UserAccount>(errors);
        }

        if (!await setupCodes.MarkUsedAsync(request.SetupCode!, cancellationToken))
        {
            return Result.Failure<UserAccount>(UserErrors.SetupCodeWrong);
        }

        if (existing is not null)
        {
            await accounts.SetPasswordAsync(existing.Id, request.Password!, mustChangePassword: false, cancellationToken);
            if (!existing.IsActive)
            {
                await accounts.UpdateAsync(existing.Id, displayName!, existing.Username, existing.Role, isActive: true, cancellationToken);
            }
            return Result.Success((await accounts.FindAsync(existing.Id, cancellationToken))!);
        }

        return await accounts.CreateAsync(
            displayName!, username!, UserRole.HouseholdAdmin, request.Password!, mustChangePassword: false, cancellationToken);
    }
}
