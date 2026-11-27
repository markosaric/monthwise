using Monthwise.Domain.Common;

namespace Monthwise.Domain.Users;

/// <summary>A user as the rules see it: enough to check the household admin rule (login FR-6).</summary>
public sealed record UserSnapshot(Guid Id, UserRole Role, bool IsActive);

/// <summary>Rules for user attributes U1 to U5 and the household admin rule (login FR-6).</summary>
public static class UserRules
{
    public const int DisplayNameMaxLength = 100;
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;

    /// <summary>The characters a username may contain besides letters and digits.</summary>
    public const string UsernameSymbols = ".-_";

    /// <summary>The trimmed display name, or the errors.</summary>
    public static (string? Value, IReadOnlyList<DomainError> Errors) ValidateDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (null, [UserErrors.DisplayNameRequired]);
        }
        return trimmed.Length > DisplayNameMaxLength ? (null, [UserErrors.DisplayNameTooLong]) : (trimmed, []);
    }

    /// <summary>The trimmed username, or the errors (U2).</summary>
    public static (string? Value, IReadOnlyList<DomainError> Errors) ValidateUsername(string? username)
    {
        var trimmed = username?.Trim();
        var valid = trimmed is { Length: >= UsernameMinLength and <= UsernameMaxLength }
            && trimmed.All(c => char.IsLetter(c) || char.IsAsciiDigit(c) || UsernameSymbols.Contains(c));
        return valid ? (trimmed, []) : (null, [UserErrors.UsernameInvalid]);
    }

    /// <summary>Errors of a new password (U3), reported on <paramref name="field"/>.</summary>
    public static IReadOnlyList<DomainError> ValidatePassword(string? password, string field) => password switch
    {
        null => [UserErrors.PasswordTooShort(field)],
        { Length: < PasswordMinLength } => [UserErrors.PasswordTooShort(field)],
        { Length: > PasswordMaxLength } => [UserErrors.PasswordTooLong(field)],
        _ => [],
    };

    /// <summary>
    /// Whether the users after a change still have at least one active household admin. A change that
    /// leaves none is refused; with no users at all (before setup) there is nothing to keep.
    /// </summary>
    public static Result EnsureHouseholdAdminRemains(IReadOnlyCollection<UserSnapshot> before, UserSnapshot? changed, Guid changedId)
    {
        ArgumentNullException.ThrowIfNull(before);
        var after = before.Where(u => u.Id != changedId).ToList();
        if (changed is not null)
        {
            after.Add(changed);
        }
        var hadAdmin = before.Any(IsActiveAdmin);
        return hadAdmin && !after.Any(IsActiveAdmin) ? Result.Failure(UserErrors.LastHouseholdAdmin) : Result.Success();
    }

    private static bool IsActiveAdmin(UserSnapshot user) => user.IsActive && user.Role == UserRole.HouseholdAdmin;
}
