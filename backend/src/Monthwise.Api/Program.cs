using System.Reflection;
using System.Text.Json.Serialization;
using Monthwise.Api.Endpoints;
using Monthwise.Api.Hosting;
using Monthwise.Application;
using Monthwise.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddConfigurationFolder();
builder.Logging.AddFileLogging(builder.Configuration);

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
builder.Services.AddProxyHosting(builder.Configuration);
builder.Services.AddDataPolicies();

// Authentication (ASP.NET Core Identity, cookie) is added in its own feature plan.

var app = builder.Build();

// Fail fast without the card number encryption keys, except while the build generates the
// OpenAPI document (it starts the app without secrets).
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    app.Services.EnsureSensitiveDataKeys();
}

app.UseProxyHosting();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMigrationGate();
app.UseImportGate();
app.UseFrontend();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // A release waits for its database; if it never answers, stop so the service manager
    // (Docker, launchd, Task Scheduler, Container Apps) starts Monthwise again.
    var waitSeconds = app.Configuration.GetValue<int>("Database:StartupWaitSeconds");
    if (waitSeconds > 0 && !await app.Services.WaitForDatabaseAsync(TimeSpan.FromSeconds(waitSeconds)))
    {
        app.Logger.LogCritical("The database did not answer within Database:StartupWaitSeconds; stopping");
        return 1;
    }

    try
    {
        await app.Services.MigrateDatabaseAsync();
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Keep the API up so /api/health can report the failure; the migration gate stops other calls.
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
api.MapDataEndpoints();

await app.RunAsync();
return 0;

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
