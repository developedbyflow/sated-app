using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Sated.Api;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Scoring;
using Sated.Parsing;
using Sated.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserFromHttp>();
builder.Services.AddDbContext<SatedDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Sated")));

builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<SatedDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(2));

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "sated.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = context.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetValue("RateLimits:LoginPerMinute", 10),
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy<string>("email", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = context.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetValue("RateLimits:EmailPerMinute", 3),
                Window = TimeSpan.FromMinutes(1)
            }));
});

var calibration = Calibration.Load();

builder.Services.AddSingleton(calibration);
builder.Services.AddSingleton(calibration.Engine());
builder.Services.AddScoped<FoodGrading>();
builder.Services.AddScoped<FoodSwaps>();
builder.Services.AddScoped<Consents>();
builder.Services.AddScoped<Profiles>();
builder.Services.AddScoped<Accounts>();
builder.Services.AddScoped<AccountRecovery>();
builder.Services.AddEmailSender(builder.Configuration, builder.Environment);
builder.Services.AddScoped<FoodCatalogue>();
builder.Services.AddScoped<Recipes>();
builder.Services.AddScoped<Meals>();
builder.Services.AddScoped<MealParsing>();
builder.Services.AddMealParser(builder.Configuration);
builder.Services.AddScoped<Days>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
