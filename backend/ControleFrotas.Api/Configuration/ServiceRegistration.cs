using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public static class ServiceRegistration
{
    public static void AddFleetServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<FleetDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Frotas")));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "ControleFrotas.Session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddScoped<IFleetStore, FleetStore>();
        builder.Services.AddScoped<FleetService>();
        builder.Services.AddScoped<ICatalogStore, CatalogStore>();
        builder.Services.AddScoped<CatalogService>();
        builder.Services.AddScoped<IFuelStore, FuelStore>();
        builder.Services.AddScoped<FuelService>();
        builder.Services.AddSingleton(TimeProvider.System);
    }
}
