using System.Text.Json.Serialization;
using api.Auth;
using api.Infrastructure;
using Data;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.Common;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and HTTP resilience (shared with Aspire).
builder.AddServiceDefaults();

// Database: the connection string is supplied by Aspire, docker-compose or user secrets.
var connectionString = builder.Configuration.GetConnectionString("hospitaldb")
    ?? throw new InvalidOperationException(
        "Connection string 'hospitaldb' is missing. Set ConnectionStrings__hospitaldb or use .NET user secrets.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    // Degraded (still 200) when notifications failed recently or delivery has stopped (R-046).
    .AddCheck<OutboxHealthCheck>("outbox");
builder.Services.AddTransient<Seed>();

// Business services, audit logging and external integration abstractions.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
// The one place that decides which patient's data a caller may reach (ARCHITECTURE.md §3).
builder.Services.AddScoped<IPatientAccessPolicy, PatientAccessPolicy>();
builder.Services.AddHospitalServices();

// Keycloak JWT authentication and role-based authorization policies.
builder.Services.AddKeycloakAuthentication(builder.Configuration);

// Delivers outbox messages (patient notifications) after the change commits.
if (builder.Configuration.GetValue("Outbox:Enabled", true))
    builder.Services.AddHostedService<OutboxDispatcher>();

// Every write request runs in one database transaction (change + audit + revisions + outbox).
builder.Services.AddScoped<TransactionalWriteFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<TransactionalWriteFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddSwaggerWithKeycloak(builder.Configuration);

var app = builder.Build();

// "dotnet run -- seeddata" still seeds sample data, as before.
var seedRequested = args.Any(a => a.Equals("seeddata", StringComparison.OrdinalIgnoreCase));
await app.InitializeDatabaseAsync(seedRequested);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
    app.UseSwaggerWithKeycloak();

app.UseCors();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<UserSyncMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Lets WebApplicationFactory<Program> in the API tests reference the entry point.
public partial class Program { }
