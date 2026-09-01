using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RevendaPro.Global.Api.Middleware;
using RevendaPro.Global.Api.Security;
using RevendaPro.Global.Application.Configuration;
using RevendaPro.Global.Domain.Interfaces.Security;
using RevendaPro.Global.Infrastructure.Configuration;
using RevendaPro.Global.Infrastructure.Database;
using RevendaPro.Global.Infrastructure.Database.Contexts;
using RevendaPro.Global.Infrastructure.Screens;
using RevendaPro.Global.Shared.Settings;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Configuration section \"Jwt\" is missing.");

if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 characters. Set REVENDAPRO_JWT_KEY in .env.");
}

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:3100"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await PrepareDatabaseAsync(app);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

await app.RunAsync();

/// <summary>
/// Applies the migrations, synchronizes the screen catalog and seeds the initial data.
/// Idempotent: starting the API twice duplicates nothing. See ADR-0002.
/// </summary>
static async Task PrepareDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    var context = services.GetRequiredService<RevendaProDbContext>();

    await WaitForDatabaseAsync(context, logger);

    // The only place Entity Framework runs at all: creating the schema. Every read and
    // write from here on goes through Dapper. See ADR-0003.
    await services.GetRequiredService<SchemaMigrator>().ApplyAsync();

    // Order matters: the screens must exist before the roles that grant them.
    await services.GetRequiredService<ScreenSynchronizer>().RunAsync();
    await services.GetRequiredService<DbInitializer>().RunAsync();

    logger.LogInformation("Database ready.");
}

/// <summary>The MariaDB container usually takes a few seconds longer than the API.</summary>
static async Task WaitForDatabaseAsync(RevendaProDbContext context, ILogger logger)
{
    const int attempts = 30;

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        if (await context.Database.CanConnectAsync())
        {
            return;
        }

        logger.LogInformation("Waiting for the database... ({Attempt}/{Total})", attempt, attempts);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    throw new InvalidOperationException("Could not connect to the database.");
}
