using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Monthwise.Application.Common;
using Monthwise.Application.Localization;
using Monthwise.Domain.Users;
using Monthwise.Infrastructure.Persistence;
using Monthwise.Infrastructure.Users;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Sign-in with built-in accounts (docs/feature-requirements/login.md, ADR 0008): the Identity cookie,
/// the session keys in the database, the policies, the sign-in rate limit and the request header check.
/// Every /api endpoint needs a signed-in user unless it is marked anonymous on purpose.
/// </summary>
internal static class LoginHosting
{
    /// <summary>A household admin without a temporary password: manages users, exports and imports (FR-6, FR-10).</summary>
    public const string HouseholdAdminPolicy = "HouseholdAdmin";

    /// <summary>The rate limit of sign-in and setup (FR-3).</summary>
    public const string SignInRateLimit = "SignIn";

    /// <summary>The header every changing API call carries; other sites cannot send it (NFR-4).</summary>
    public const string RequestHeader = "X-Monthwise-Request";

    public const string CookieName = "monthwise.session";
    public const string DisplayNameClaim = "monthwise:display_name";
    public const string MustChangePasswordClaim = "monthwise:must_change_password";

    /// <summary>Without "Stay signed in", the session ends this long after signing in (FR-4).</summary>
    public static readonly TimeSpan ShortSessionLength = TimeSpan.FromHours(12);

    /// <summary>With "Stay signed in", the session lasts this long from the last use (FR-4).</summary>
    public static readonly TimeSpan LongSessionLength = TimeSpan.FromDays(30);

    private const string EndsAtItem = ".monthwise.endsAt";
    private const string AttemptsPerMinuteSetting = "Login:SignInAttemptsPerMinute";
    private const int DefaultAttemptsPerMinute = 10;

    public static IServiceCollection AddLogin(this IServiceCollection services, IConfiguration configuration)
    {
        // The keys that protect the cookie live in the database, so sessions survive restarts,
        // updates and Azure starts after scaling to zero (FR-4).
        services.AddDataProtection()
            .SetApplicationName("Monthwise")
            .PersistKeysToDbContext<MonthwiseDbContext>();

        new IdentityBuilder(typeof(AppUser), services)
            .AddSignInManager()
            .AddClaimsPrincipalFactory<AppUserClaimsFactory>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.Configure<SecurityStampValidatorOptions>(options =>
            // Checked on every request: a deactivated or deleted user, a password reset and a role change
            // reach open sessions at once (FR-6, FR-7). One small query per request.
            options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // HTTPS only where the request is HTTPS (Azure); plain HTTP on the home network works too (NFR-5).
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = LongSessionLength;
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden);
            options.Events.OnValidatePrincipal = ValidatePrincipalAsync;
        });

        // Every /api endpoint needs the default policy (Program.cs); requests that match no endpoint,
        // such as screen addresses and files, are not affected.
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(SignedInWithOwnPassword().Build())
            .AddPolicy(HouseholdAdminPolicy, policy => SignedInWithOwnPassword(policy).RequireRole(nameof(UserRole.HouseholdAdmin)));
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();

        var attemptsPerMinute = configuration.GetValue(AttemptsPerMinuteSetting, DefaultAttemptsPerMinute);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(TypedResults.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: ServerTexts.Translate("Too many sign-in attempts. Wait a minute and try again."))
                .ExecuteAsync(context.HttpContext));
            options.AddPolicy(SignInRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = attemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        return services;
    }

    /// <summary>
    /// Refuses changing API calls without <see cref="RequestHeader"/> (NFR-4), then signs in from the cookie,
    /// applies the sign-in rate limit and the policies.
    /// </summary>
    public static WebApplication UseLogin(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                && !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
                && !request.Headers.ContainsKey(RequestHeader))
            {
                await TypedResults.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: ServerTexts.Translate("This request did not come from Monthwise's screens."),
                        extensions: new Dictionary<string, object?> { ["code"] = "request.header_missing" })
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }

    /// <summary>The properties of a new session (FR-4): long and renewed on use, or ending 12 hours after sign-in.</summary>
    public static AuthenticationProperties SessionProperties(bool staySignedIn, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var properties = new AuthenticationProperties { IsPersistent = staySignedIn };
        if (!staySignedIn)
        {
            properties.Items[EndsAtItem] = (timeProvider.GetUtcNow() + ShortSessionLength).ToString("O", CultureInfo.InvariantCulture);
        }
        return properties;
    }

    /// <summary>
    /// Marks the endpoints a user with a temporary password may use: changing the password and signing
    /// out (FR-7). Every other endpoint waits until the password is changed.
    /// </summary>
    public static TBuilder AllowTemporaryPassword<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new TemporaryPasswordAllowed());

    private static AuthorizationPolicyBuilder SignedInWithOwnPassword(AuthorizationPolicyBuilder? policy = null) =>
        (policy ?? new AuthorizationPolicyBuilder())
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                !context.User.HasClaim(MustChangePasswordClaim, bool.TrueString)
                || (context.Resource as HttpContext)?.GetEndpoint()?.Metadata.GetMetadata<TemporaryPasswordAllowed>() is not null);

    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null)
        {
            return;
        }

        var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (context.Properties.Items.TryGetValue(EndsAtItem, out var endsAt)
            && DateTimeOffset.TryParse(endsAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end)
            && now >= end)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode) =>
        statusCode == StatusCodes.Status401Unauthorized
            ? TypedResults.Problem(
                    statusCode: statusCode,
                    title: ServerTexts.Translate("Sign in to continue."),
                    extensions: new Dictionary<string, object?> { ["code"] = "auth.signed_out" })
                .ExecuteAsync(context)
            : context.User.HasClaim(MustChangePasswordClaim, bool.TrueString)
                ? TypedResults.Problem(
                        statusCode: statusCode,
                        title: ServerTexts.Translate("Choose a new password first."),
                        extensions: new Dictionary<string, object?> { ["code"] = "auth.password_change_required" })
                    .ExecuteAsync(context)
                : TypedResults.Problem(
                        statusCode: statusCode,
                        title: ServerTexts.Translate("Only a household admin can do this."),
                        extensions: new Dictionary<string, object?> { ["code"] = "auth.household_admin_only" })
                    .ExecuteAsync(context);

    private sealed record TemporaryPasswordAllowed;

    /// <summary>401 and 403 as problem details in the request's language, never a redirect (FR-2).</summary>
    private sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Challenged)
            {
                return WriteProblemAsync(context, StatusCodes.Status401Unauthorized);
            }
            if (authorizeResult.Forbidden)
            {
                return context.User.Identity?.IsAuthenticated == true
                    ? WriteProblemAsync(context, StatusCodes.Status403Forbidden)
                    : WriteProblemAsync(context, StatusCodes.Status401Unauthorized);
            }
            return _default.HandleAsync(next, context, policy, authorizeResult);
        }
    }

    /// <summary>The claims of a session: id, username, security stamp, display name, role and a temporary password.</summary>
    private sealed class AppUserClaimsFactory(UserManager<AppUser> userManager, IOptions<IdentityOptions> options)
        : UserClaimsPrincipalFactory<AppUser>(userManager, options)
    {
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim(DisplayNameClaim, user.DisplayName));
            identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, user.Role.ToString()));
            if (user.MustChangePassword)
            {
                identity.AddClaim(new Claim(MustChangePasswordClaim, bool.TrueString));
            }
            return identity;
        }
    }

    /// <summary>The signed-in user from the request's claims.</summary>
    private sealed class HttpCurrentUser(IHttpContextAccessor accessor, IOptions<IdentityOptions> options) : ICurrentUser
    {
        private ClaimsPrincipal? User => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

        public Guid? Id => Guid.TryParse(User?.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType), out var id) ? id : null;

        public string? DisplayName => User?.FindFirstValue(DisplayNameClaim);

        public UserRole? Role => Enum.TryParse<UserRole>(User?.FindFirstValue(options.Value.ClaimsIdentity.RoleClaimType), out var role)
            ? role
            : null;
    }
}
