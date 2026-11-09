using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database (SQL Server via EF Core, Code-First)
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

// Azure Blob Storage for event banner images (configure AzureBlobStorage:ConnectionString)
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("AzureBlobStorage"));
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// Session-based authentication: the cookie IS the session and carries the user id + role
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDay.Session";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;

        // An API should answer with status codes, not redirect to a login page
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "API for managing race events, categories, enrolments and results. " +
                      "Call POST /api/auth/login first - the session cookie is then sent automatically by Swagger UI."
    });
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile)) c.IncludeXmlComments(xmlFile);
});

var app = builder.Build();

// Create the database and tables from the EF Core model on first run (the roles are seeded too).
// Skipped in the automated tests, which use an in-memory database.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // In Docker the database server can take a while to accept connections, so retry for a bit
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            UpgradeSchema(db);
            break;
        }
        catch (Exception ex) when (attempt < 20)
        {
            logger.LogWarning("Database not ready (attempt {Attempt}/20): {Message}", attempt, ex.Message);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();

// EnsureCreated never alters existing tables, so databases created before the Part 3 changes
// (banner image + enrolment status) get the new columns added here. Safe to run repeatedly.
static void UpgradeSchema(RaceDayDbContext db)
{
    db.Database.ExecuteSqlRaw(
        "IF COL_LENGTH('dbo.Event', 'BannerBlobName') IS NULL ALTER TABLE dbo.Event ADD BannerBlobName VARCHAR(260) NULL;");
    db.Database.ExecuteSqlRaw(
        "IF COL_LENGTH('dbo.Enrolment', 'Status') IS NULL " +
        "ALTER TABLE dbo.Enrolment ADD Status VARCHAR(20) NOT NULL CONSTRAINT DF_Enrolment_Status DEFAULT 'Pending';");
}

// Needed so the test project can use WebApplicationFactory<Program>
public partial class Program { }
