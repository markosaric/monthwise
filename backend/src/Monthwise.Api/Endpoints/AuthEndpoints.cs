using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Monthwise.Api.Hosting;
using Monthwise.Application.Common;
using Monthwise.Application.Users;
using Monthwise.Domain.Common;
using Monthwise.Domain.Users;
using Monthwise.Infrastructure.Users;

namespace Monthwise.Api.Endpoints;

/// <summary>
/// Setup, signing in and out and changing one's own password (docs/feature-requirements/login.md FR-1 to
/// FR-5, FR-8, FR-9). Passwords and setup codes are never logged; sign-ins are logged with the username only.
/// </summary>
/// <summary>The log category of sign-in, setup and user changes.</summary>
internal sealed class LoginLog;

internal static partial class AuthEndpoints
{
    private static readonly object DummyHashLock = new();
    private static string? _dummyHash;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/auth").WithTags("Sign in");

        auth.MapGet("/state", GetStateAsync)
            .WithName("GetAuthState")
            .WithSummary("Whether the installation still needs its first household admin, and who is signed in, if anybody.")
            .AllowAnonymous();

        auth.MapPost("/setup", SetupAsync)
            .WithName("SetUp")
            .WithSummary("With the installation's setup code: creates the first household admin, or later a new admin or a new admin password; signs in.")
            .AllowAnonymous()
            .RequireRateLimiting(LoginHosting.SignInRateLimit);

        auth.MapPost("/sign-in", SignInAsync)
            .WithName("SignIn")
            .WithSummary("Signs in with username and password; with staySignedIn the session lasts 30 days from the last use.")
            .AllowAnonymous()
            .RequireRateLimiting(LoginHosting.SignInRateLimit)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/sign-out", async (SignInManager<AppUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.NoContent();
            })
            .WithName("SignOut")
            .WithSummary("Ends the session on this browser.")
            .AllowTemporaryPassword();

        auth.MapPost("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithSummary("Changes the signed-in user's own password; ends their other sessions.")
            .AllowTemporaryPassword();

        return routes;
    }

    private static async Task<AuthStateResponse> GetStateAsync(IUserAccounts accounts, ICurrentUser currentUser, CancellationToken ct)
    {
        var user = currentUser.Id is { } id ? await accounts.FindAsync(id, ct) : null;
        return new AuthStateResponse(!await accounts.AnyAsync(ct), user?.ToResponse());
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> SetupAsync(
        SetupRequest request,
        SetupHandler handler,
        SignInManager<AppUser> signInManager,
        TimeProvider timeProvider,
        ILogger<LoginLog> logger,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        if (result.IsFailure)
        {
            return result.ToNoContent();
        }

        var user = await signInManager.UserManager.FindByIdAsync(result.Value.Id.ToString());
        await signInManager.SignInAsync(user!, LoginHosting.SessionProperties(staySignedIn: false, timeProvider));
        LogSetupUsed(logger, result.Value.Username);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> SignInAsync(
        SignInRequest request,
        SignInManager<AppUser> signInManager,
        TimeProvider timeProvider,
        ILogger<LoginLog> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        var userManager = signInManager.UserManager;
        var user = username.Length == 0 ? null : await userManager.FindByNameAsync(username);
        if (user is null || !user.IsActive)
        {
            // The same work and the same answer as a wrong password, so the answer does not tell whether the username exists.
            userManager.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyHash(userManager), password);
            LogSignInFailed(logger, username);
            return Result.Failure(UserErrors.SignInFailed).ToNoContent();
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            LogSignInLockedOut(logger, username);
            return Result.Failure(UserErrors.SignInLockedOut).ToNoContent();
        }
        if (!check.Succeeded)
        {
            LogSignInFailed(logger, username);
            return Result.Failure(UserErrors.SignInFailed).ToNoContent();
        }

        await signInManager.SignInAsync(user, LoginHosting.SessionProperties(request.StaySignedIn, timeProvider));
        LogSignedIn(logger, user.UserName ?? username);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ChangePasswordAsync(
        PasswordChangeRequest request,
        ChangePasswordHandler handler,
        ICurrentUser currentUser,
        SignInManager<AppUser> signInManager,
        ILogger<LoginLog> logger,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        if (result.IsFailure)
        {
            return result.ToNoContent();
        }

        // The new security stamp ends the other sessions; this one gets it, so it stays signed in.
        var user = await signInManager.UserManager.FindByIdAsync(currentUser.Id!.Value.ToString());
        await signInManager.RefreshSignInAsync(user!);
        LogPasswordChanged(logger, user!.UserName ?? string.Empty);
        return TypedResults.NoContent();
    }

    private static string DummyHash(UserManager<AppUser> userManager)
    {
        lock (DummyHashLock)
        {
            return _dummyHash ??= userManager.PasswordHasher.HashPassword(new AppUser(), Guid.NewGuid().ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Signed in: {Username}")]
    private static partial void LogSignedIn(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed: {Username}")]
    private static partial void LogSignInFailed(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in refused, locked out for a while: {Username}")]
    private static partial void LogSignInLockedOut(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Setup code used for household admin {Username}")]
    private static partial void LogSetupUsed(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password changed: {Username}")]
    private static partial void LogPasswordChanged(ILogger logger, string username);
}
