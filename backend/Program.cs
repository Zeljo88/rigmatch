using RigMatch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<ICvTextExtractionService, CvTextExtractionService>();
builder.Services.AddHttpClient<ICvParsingService, CvParsingService>();
builder.Services.Configure<CvParsingOptions>(builder.Configuration.GetSection("CvParsing"));
const string FrontendCorsPolicy = "FrontendDevPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(FrontendCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
