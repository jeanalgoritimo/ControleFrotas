using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public static class DatabaseBootstrap
{
    public static async Task InitializeFleetDatabase(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetDb>();
            await db.Database.MigrateAsync();
            if (!await db.Accounts.AnyAsync())
            {
                var username = app.Configuration["Bootstrap:Username"];
                var password = app.Configuration["Bootstrap:Password"];
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || password.Length < 12)
                    throw new InvalidOperationException("Primeiro acesso: defina Bootstrap__Username e Bootstrap__Password (mínimo 12 caracteres). Consulte README.");
                var account = new Account { Username = username.Trim().ToLowerInvariant() };
                account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, password);
                db.Accounts.Add(account);
                await db.SaveChangesAsync();
            }
        }
    }
}
