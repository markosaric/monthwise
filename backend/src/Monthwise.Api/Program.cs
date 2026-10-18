using System.Reflection;
using System.Text.Json.Serialization;
using Monthwise.Api.Endpoints;
using Monthwise.Application;
using Monthwise.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UniqueConstraintExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Amounts in cents are plain JSON numbers (and typed as numbers in the generated client).
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddOpenApi();

// Authentication (ASP.NET Core Identity, cookie) is added in its own feature plan.

var app = builder.Build();

// Fail fast without the card number encryption keys, except while the build generates the
// OpenAPI document (it starts the app without secrets).
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    app.Services.EnsureSensitiveDataKeys();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    try
    {
        await app.Services.MigrateDatabaseAsync();
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Keep the API up so /api/health can report the database as unhealthy.
        app.Logger.LogError(ex, "Applying database migrations failed");
    }
}

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapCardEndpoints();
api.MapCashFlowEndpoints();
api.MapLoanEndpoints();
api.MapCardPurchaseEndpoints();
api.MapExpenseEndpoints();
api.MapSharedCostEndpoints();
api.MapDashboardEndpoints();

await app.RunAsync();

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
