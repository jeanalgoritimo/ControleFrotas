using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext c, IAntiforgery a) => Results.Ok(new { token = a.GetAndStoreTokens(c).RequestToken }));
        app.MapPost("/api/auth/login", async (LoginRequest r, FleetDb db, HttpContext c) =>
        {
            var username = (r.Username ?? "").Trim().ToLowerInvariant();
            var account = await db.Accounts.SingleOrDefaultAsync(x => x.Username == username);
            if (account is null || string.IsNullOrEmpty(r.Password) || new PasswordHasher<Account>().VerifyHashedPassword(account, account.PasswordHash, r.Password) == PasswordVerificationResult.Failed)
                return Results.Json(new { message = "Usuário ou senha inválidos." }, statusCode: 401);
            var claims = new[] { new Claim(ClaimTypes.Name, account.Username), new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim("company", account.CompanyId.ToString()), new Claim(ClaimTypes.Role, "Administrador") };
            await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok(new { name = account.Username });
        }).RequireRateLimiting("login");
        app.MapGet("/api/auth/me", (HttpContext c) => Results.Ok(new { name = c.User.Identity!.Name })).RequireAuthorization();
        app.MapPost("/api/auth/logout", async (HttpContext c) => { await c.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
    }
}
