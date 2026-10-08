using System.Security.Claims;
using System.Threading.RateLimiting;
using ControleFrotas;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
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
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FleetDb>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Accounts.AnyAsync())
    {
        var username = builder.Configuration["Bootstrap:Username"];
        var password = builder.Configuration["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || password.Length < 12)
            throw new InvalidOperationException("Primeiro acesso: defina Bootstrap__Username e Bootstrap__Password (mínimo 12 caracteres). Consulte README.");
        var account = new Account { Username = username.Trim().ToLowerInvariant() };
        account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, password);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
    }
}
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (DbUpdateConcurrencyException) { await Error(context, 409, "Registro alterado por outra sessão. Atualize e tente novamente."); }
    catch (DbUpdateException) { await Error(context, 409, "Não foi possível gravar. Verifique duplicidade de placa ou CPF."); }
    catch (AntiforgeryValidationException) { await Error(context, 400, "Sessão de formulário inválida. Recarregue a página."); }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Falha ao processar {Path}", context.Request.Path);
        await Error(context, 500, "Falha interna. Consulte o log do servidor.");
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (new[] { "POST", "PUT", "DELETE", "PATCH" }.Contains(context.Request.Method))
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next();
});
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
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/vehicles", async (FleetDb db, HttpContext c) => await db.Vehicles.AsNoTracking().Where(x => x.CompanyId == IdentityScope.Company(c)).OrderBy(x => x.Plate).ToListAsync());
api.MapPost("/vehicles", async (VehicleRequest r, FleetDb db, HttpContext c) =>
{
    if (Validation.VehicleError(r) is { } error) return Results.BadRequest(new { message = error });
    await using var transaction = await db.Database.BeginTransactionAsync();
    var v = new Vehicle { CompanyId = IdentityScope.Company(c) }; ApplyVehicle(v, r); db.Vehicles.Add(v);
    await db.SaveChangesAsync();
    db.Audits.Add(Log(c, "Veículo criado", v.Id)); await db.SaveChangesAsync(); await transaction.CommitAsync();
    return Results.Created($"/api/vehicles/{v.Id}", v);
});
api.MapPut("/vehicles/{id:int}", async (int id, VehicleRequest r, FleetDb db, HttpContext c) =>
{
    if (Validation.VehicleError(r) is { } error) return Results.BadRequest(new { message = error });
    var v = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == IdentityScope.Company(c));
    if (v is null) return Results.NotFound();
    if (r.Version is null) return Results.BadRequest(new { message = "Versão do registro obrigatória." });
    if (r.Odometer < v.Odometer) return Results.BadRequest(new { message = "Hodômetro não pode diminuir nesta edição." });
    db.Entry(v).Property(x => x.Version).OriginalValue = r.Version;
    ApplyVehicle(v, r); db.Audits.Add(Log(c, "Veículo editado", id)); await db.SaveChangesAsync(); return Results.Ok(v);
});
api.MapGet("/drivers", async (FleetDb db, HttpContext c) => await db.Drivers.AsNoTracking().Where(x => x.CompanyId == IdentityScope.Company(c)).OrderBy(x => x.Name).ToListAsync());
api.MapPost("/drivers", async (DriverRequest r, FleetDb db, HttpContext c) =>
{
    if (Validation.DriverError(r) is { } error) return Results.BadRequest(new { message = error });
    await using var transaction = await db.Database.BeginTransactionAsync();
    var d = new Driver { CompanyId = IdentityScope.Company(c) }; ApplyDriver(d, r); db.Drivers.Add(d); await db.SaveChangesAsync();
    db.Audits.Add(Log(c, "Motorista criado", d.Id)); await db.SaveChangesAsync(); await transaction.CommitAsync(); return Results.Created($"/api/drivers/{d.Id}", d);
});
api.MapPut("/drivers/{id:int}", async (int id, DriverRequest r, FleetDb db, HttpContext c) =>
{
    if (Validation.DriverError(r) is { } error) return Results.BadRequest(new { message = error });
    var d = await db.Drivers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == IdentityScope.Company(c));
    if (d is null) return Results.NotFound();
    if (r.Version is null) return Results.BadRequest(new { message = "Versão do registro obrigatória." });
    db.Entry(d).Property(x => x.Version).OriginalValue = r.Version;
    ApplyDriver(d, r); db.Audits.Add(Log(c, "Motorista editado", id)); await db.SaveChangesAsync(); return Results.Ok(d);
});
api.MapGet("/audit", async (FleetDb db, HttpContext c) => await db.Audits.AsNoTracking().Where(x => x.CompanyId == IdentityScope.Company(c)).OrderByDescending(x => x.Id).Take(100).ToListAsync());
app.Run();
static Audit Log(HttpContext c, string action, int id) => new() { CompanyId = IdentityScope.Company(c), Actor = c.User.Identity!.Name!, Action = action, RecordId = id };
static void ApplyVehicle(Vehicle v, VehicleRequest r) { v.Plate = Validation.Plate(r.Plate); v.Category = r.Category; v.Brand = r.Brand.Trim(); v.Model = r.Model.Trim(); v.Year = r.Year; v.Odometer = r.Odometer; v.Active = r.Active; }
static void ApplyDriver(Driver d, DriverRequest r) { d.Name = r.Name.Trim(); d.Cpf = r.Cpf; d.License = r.License; d.LicenseCategory = r.LicenseCategory; d.LicenseExpiry = r.LicenseExpiry; d.Active = r.Active; }
static async Task Error(HttpContext c, int status, string message) { c.Response.StatusCode = status; await c.Response.WriteAsJsonAsync(new { message }); }
