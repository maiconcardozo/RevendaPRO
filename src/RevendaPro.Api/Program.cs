using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RevendaPro.Api.Middleware;
using RevendaPro.Api.Security;
using RevendaPro.Application.Configuration;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Infrastructure.Configuration;
using RevendaPro.Infrastructure.Database;
using RevendaPro.Infrastructure.Screens;
using RevendaPro.Infrastructure.Storage;
using RevendaPro.Shared.Settings;

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

    // Waits for the database and applies the migrations. Entity Framework runs only in
    // there, to generate the SQL; every read and write from here on goes through Dapper.
    // See ADR-0003.
    await services.GetRequiredService<SchemaMigrator>().ApplyAsync();

    // Order matters: the screens must exist before the roles that grant them.
    await services.GetRequiredService<ScreenSynchronizer>().RunAsync();
    await services.GetRequiredService<DbInitializer>().RunAsync();

    // Creates the buckets when configured to, which is local development only. Storage lives
    // outside the database on purpose: the row keeps the key, and never the bytes. See ADR-0004.
    await services.GetRequiredService<StorageInitializer>().RunAsync();

    logger.LogInformation("Database ready.");
}

