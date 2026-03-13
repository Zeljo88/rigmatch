using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using RigMatch.Api.Data.Entities;
using RigMatch.Api.Data;
using RigMatch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<RigMatchDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=rigmatch.db";
    if (IsPostgresConnectionString(connectionString))
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
        return;
    }

    options.UseSqlite(connectionString);
});
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<ICvDiagnosticsLogger, FileCvDiagnosticsLogger>();
builder.Services.AddSingleton<ICvParsingGate, CvParsingGate>();
builder.Services.AddScoped<IPasswordHasher<EmployerUser>, PasswordHasher<EmployerUser>>();
builder.Services.AddScoped<LocalFileStorageService>();
builder.Services.AddScoped<AzureBlobFileStorageService>();
builder.Services.AddScoped<IFileStorageService>(serviceProvider =>
{
    var storageOptions = serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
    return string.Equals(storageOptions.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase)
        ? serviceProvider.GetRequiredService<AzureBlobFileStorageService>()
        : serviceProvider.GetRequiredService<LocalFileStorageService>();
});
builder.Services.AddScoped<ICvTextExtractionService, CvTextExtractionService>();
builder.Services.AddScoped<IRoleStandardizationService, RoleStandardizationService>();
builder.Services.AddScoped<IParsingReferenceService, ParsingReferenceService>();
builder.Services.AddScoped<IProjectMatchingService, ProjectMatchingService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddHttpClient<ICvParsingService, CvParsingService>();
builder.Services.Configure<CvParsingOptions>(builder.Configuration.GetSection("CvParsing"));
const string FrontendCorsPolicy = "FrontendDevPolicy";
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            IsAllowedOrigin(origin, configuredOrigins, builder.Environment.IsDevelopment()))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RigMatchDbContext>();
    await RigMatchDbBootstrapper.InitializeAsync(dbContext);
}

// Configure the HTTP request pipeline.

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));
app.MapControllers();

app.Run();

static bool IsPostgresConnectionString(string connectionString)
{
    return connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
           connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
           connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase);
}

static bool IsAllowedOrigin(string origin, IReadOnlyCollection<string> configuredOrigins, bool isDevelopment)
{
    if (configuredOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!isDevelopment)
    {
        return false;
    }

    return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;
}
