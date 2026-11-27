using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Monthwise.Application.Users;
using Monthwise.Domain.Common;
using Monthwise.Domain.Users;
using Monthwise.Infrastructure.Persistence;
using Monthwise.Infrastructure.Security;
using Npgsql;

namespace Monthwise.Infrastructure.Users;

/// <summary>User accounts through ASP.NET Core Identity's user manager (login FR-6 to FR-9, ADR 0008).</summary>
internal sealed class UserAccounts(UserManager<AppUser> userManager) : IUserAccounts
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken) => userManager.Users.AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken) =>
        (await userManager.Users.AsNoTracking().ToListAsync(cancellationToken)).Select(u => u.ToAccount()).ToList();

    public async Task<UserAccount?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        (await userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken))?.ToAccount();

    public async Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = userManager.NormalizeName(username);
        return (await userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, cancellationToken))
            ?.ToAccount();
    }

    public async Task<Result<UserAccount>> CreateAsync(
        string displayName, string username, UserRole role, string password, bool mustChangePassword, CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = username,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            MustChangePassword = mustChangePassword,
        };
        var result = await userManager.CreateAsync(user, password);
        return result.Succeeded ? Result.Success(user.ToAccount()) : Result.Failure<UserAccount>(ToErrors(result));
    }

    public async Task<Result<UserAccount>> UpdateAsync(
        Guid id, string displayName, string username, UserRole role, bool isActive, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(id);
        var deactivated = user.IsActive && !isActive;
        user.DisplayName = displayName;
        user.UserName = username;
        user.Role = role;
        user.IsActive = isActive;
        if (deactivated)
        {
            // A new security stamp ends the user's open sessions on their next request.
            user.SecurityStamp = NewStamp();
        }

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Result.Success(user.ToAccount()) : Result.Failure<UserAccount>(ToErrors(result));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(id);
        Ensure(await userManager.DeleteAsync(user));
    }

    public async Task SetPasswordAsync(Guid id, string password, bool mustChangePassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(id);
        SetPassword(user, password, mustChangePassword);
        Ensure(await userManager.UpdateAsync(user));
    }

    public async Task<Result> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(id);
        if (!await userManager.CheckPasswordAsync(user, currentPassword))
        {
            return Result.Failure(UserErrors.CurrentPasswordWrong);
        }

        SetPassword(user, newPassword, mustChangePassword: false);
        Ensure(await userManager.UpdateAsync(user));
        return Result.Success();
    }

    /// <summary>The password rules are checked by <see cref="UserRules"/> before; Identity only hashes it here.</summary>
    private void SetPassword(AppUser user, string password, bool mustChangePassword)
    {
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, password);
        user.MustChangePassword = mustChangePassword;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.SecurityStamp = NewStamp();
    }

    private async Task<AppUser> RequireAsync(Guid id) =>
        await userManager.FindByIdAsync(id.ToString()) ?? throw new InvalidOperationException("The user does not exist.");

    private static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20));

    private static IEnumerable<DomainError> ToErrors(IdentityResult result) =>
        result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
            ? [UserErrors.UsernameTaken]
            : throw new InvalidOperationException($"Identity refused the user: {string.Join(", ", result.Errors.Select(e => e.Code))}");

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Identity refused the change: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
    }
}

/// <summary>
/// The setup code from the setting <c>Setup:Code</c> (a secret, like the encryption keys), compared without
/// regard to case, spaces and dashes; a used code is remembered by its keyed hash in <c>setup_code_use</c>.
/// </summary>
internal sealed class SetupCodes(IConfiguration configuration, ISensitiveDataCipher cipher, MonthwiseDbContext dbContext, TimeProvider timeProvider)
    : ISetupCodes
{
    public const string SettingName = "Setup:Code";

    private readonly string? _configured = Normalize(configuration[SettingName]);

    public bool IsConfigured => _configured is not null;

    public async Task<bool> IsUsableAsync(string? code, CancellationToken cancellationToken)
    {
        var normalized = Normalize(code);
        if (_configured is null || normalized is null)
        {
            return false;
        }

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(cipher.Hash(normalized)), Encoding.UTF8.GetBytes(cipher.Hash(_configured)));
        if (!matches)
        {
            return false;
        }

        var hash = cipher.Hash(_configured);
        return !await dbContext.SetupCodeUses.AnyAsync(u => u.CodeHash == hash, cancellationToken);
    }

    public async Task<bool> MarkUsedAsync(string code, CancellationToken cancellationToken)
    {
        if (!await IsUsableAsync(code, cancellationToken))
        {
            return false;
        }

        var use = new SetupCodeUse { Id = Guid.CreateVersion7(), CodeHash = cipher.Hash(_configured!), UsedAt = timeProvider.GetUtcNow() };
        dbContext.SetupCodeUses.Add(use);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Used by another request at the same moment.
            dbContext.Entry(use).State = EntityState.Detached;
            return false;
        }
    }

    private static string? Normalize(string? code)
    {
        var normalized = code is null
            ? null
            : new string(code.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
