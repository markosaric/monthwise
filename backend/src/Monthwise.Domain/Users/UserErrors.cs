using Monthwise.Domain.Common;

namespace Monthwise.Domain.Users;

/// <summary>Errors of the user and sign-in rules (login). Field names match the API request fields.</summary>
public static class UserErrors
{
    public static readonly DomainError DisplayNameRequired = new("user.displayName.required", "Display name is required.", "displayName");
    public static readonly DomainError DisplayNameTooLong = new(
        "user.displayName.tooLong", $"Display name can be at most {UserRules.DisplayNameMaxLength} characters.", "displayName");
    public static readonly DomainError UsernameInvalid = new(
        "user.username.invalid",
        $"Username must be {UserRules.UsernameMinLength} to {UserRules.UsernameMaxLength} characters: letters, digits, dot, dash or underscore.",
        "username");
    public static readonly DomainError UsernameTaken = new("user.username.taken", "This username is already used.", "username");

    public static DomainError PasswordTooShort(string field) => new(
        "user.password.tooShort", $"The password must be at least {UserRules.PasswordMinLength} characters long.", field);
    public static DomainError PasswordTooLong(string field) => new(
        "user.password.tooLong", $"The password can be at most {UserRules.PasswordMaxLength} characters long.", field);

    public static readonly DomainError CurrentPasswordWrong = new("user.currentPassword.wrong", "The current password is not right.", "currentPassword");
    public static readonly DomainError LastHouseholdAdmin = new(
        "user.lastHouseholdAdmin", "There must always be at least one active household admin.", Kind: ErrorKind.Conflict);
    public static readonly DomainError UserNotFound = new("user.notFound", "User not found.", Kind: ErrorKind.NotFound);

    public static readonly DomainError SignInFailed = new("signIn.failed", "The username or password is not right.");
    public static readonly DomainError SignInLockedOut = new(
        "signIn.lockedOut", "Signing in with this username is locked for a while. Try again later.");

    public static readonly DomainError SetupCodeNotConfigured = new(
        "setup.code.notConfigured", "This installation has no setup code. See the user manual, chapter Sign in and users.", "setupCode");
    public static readonly DomainError SetupCodeWrong = new("setup.code.wrong", "The setup code is not right or was already used.", "setupCode");
    public static readonly DomainError SetupUsernameNotAdmin = new(
        "setup.username.notAdmin", "This username belongs to a member. Choose a household admin or a new username.", "username");
}
