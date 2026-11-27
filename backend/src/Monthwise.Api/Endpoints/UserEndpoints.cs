using Monthwise.Api.Hosting;
using Monthwise.Application.Users;

namespace Monthwise.Api.Endpoints;

/// <summary>The users of the household (docs/feature-requirements/login.md FR-6, FR-7). Changes are for household admins.</summary>
internal static partial class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var users = routes.MapGroup("/users").WithTags("Users");

        users.MapGet("/", (UsersHandler handler, CancellationToken ct) => handler.ListAsync(ct))
            .WithName("GetUsers")
            .WithSummary("Every user for a household admin; only oneself for a member.");

        users.MapGet("/{id:guid}", async (Guid id, UsersHandler handler, CancellationToken ct) => (await handler.GetAsync(id, ct)).ToOk())
            .WithName("GetUser")
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPost("/", async (NewUserRequest request, UsersHandler handler, ILogger<LoginLog> logger, CancellationToken ct) =>
            {
                var result = await handler.CreateAsync(request, ct);
                if (result.IsSuccess)
                {
                    LogUserAdded(logger, result.Value.Username);
                }
                return result.ToCreated(u => $"/api/users/{u.Id}");
            })
            .WithName("CreateUser")
            .WithSummary("Adds a user with a temporary password, which they must change at their first sign-in.")
            .RequireAuthorization(LoginHosting.HouseholdAdminPolicy);

        users.MapPut("/{id:guid}", async (Guid id, UserRequest request, UsersHandler handler, ILogger<LoginLog> logger, CancellationToken ct) =>
            {
                var result = await handler.UpdateAsync(id, request, ct);
                if (result.IsSuccess)
                {
                    LogUserChanged(logger, result.Value.Username);
                }
                return result.ToOk();
            })
            .WithName("UpdateUser")
            .WithSummary("Changes a user; there is always at least one active household admin.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(LoginHosting.HouseholdAdminPolicy);

        users.MapDelete("/{id:guid}", async (Guid id, UsersHandler handler, ILogger<LoginLog> logger, CancellationToken ct) =>
            {
                var result = await handler.DeleteAsync(id, ct);
                if (result.IsSuccess)
                {
                    LogUserDeleted(logger, id);
                }
                return result.ToNoContent();
            })
            .WithName("DeleteUser")
            .WithSummary("Deletes a user; the last active household admin cannot be deleted.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(LoginHosting.HouseholdAdminPolicy);

        users.MapPost("/{id:guid}/password", async (
                Guid id, PasswordResetRequest request, UsersHandler handler, ILogger<LoginLog> logger, CancellationToken ct) =>
            {
                var result = await handler.ResetPasswordAsync(id, request, ct);
                if (result.IsSuccess)
                {
                    LogPasswordReset(logger, result.Value.Username);
                }
                return result.ToOk();
            })
            .WithName("ResetUserPassword")
            .WithSummary("Sets a temporary password for a user and ends all of their sessions.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(LoginHosting.HouseholdAdminPolicy);

        return routes;
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "User added: {Username}")]
    private static partial void LogUserAdded(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "User changed: {Username}")]
    private static partial void LogUserChanged(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "User deleted: {UserId}")]
    private static partial void LogUserDeleted(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Temporary password set for {Username}")]
    private static partial void LogPasswordReset(ILogger logger, string username);
}
