using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;
using RigMatch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<RigMatchDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=rigmatch.db";
    options.UseSqlite(connectionString);
});
builder.Services.AddSingleton<ICvDiagnosticsLogger, FileCvDiagnosticsLogger>();
builder.Services.AddSingleton<ICvParsingGate, CvParsingGate>();
builder.Services.AddScoped<ICvTextExtractionService, CvTextExtractionService>();
builder.Services.AddScoped<IRoleStandardizationService, RoleStandardizationService>();
builder.Services.AddHttpClient<ICvParsingService, CvParsingService>();
builder.Services.Configure<CvParsingOptions>(builder.Configuration.GetSection("CvParsing"));
const string FrontendCorsPolicy = "FrontendDevPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback)
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

app.UseAuthorization();

app.MapControllers();

app.Run();
