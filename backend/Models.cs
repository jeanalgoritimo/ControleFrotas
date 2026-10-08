using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ControleFrotas;

public class Vehicle
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(7)] public string Plate { get; set; } = "";
    [MaxLength(20)] public string Category { get; set; } = "Carro";
    [MaxLength(80)] public string Brand { get; set; } = "";
    [MaxLength(100)] public string Model { get; set; } = "";
    public int Year { get; set; }
    public decimal Odometer { get; set; }
    public bool Active { get; set; } = true;
    [Timestamp] public byte[] Version { get; set; } = [];
}
public class Driver
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(11)] public string Cpf { get; set; } = "";
    [MaxLength(11)] public string License { get; set; } = "";
    [MaxLength(2)] public string LicenseCategory { get; set; } = "B";
    public DateOnly LicenseExpiry { get; set; }
    public bool Active { get; set; } = true;
    [Timestamp] public byte[] Version { get; set; } = [];
}
public class Account
{
    public int Id { get; set; }
    public int CompanyId { get; set; } = 1;
    [MaxLength(100)] public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
public class Audit
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public int RecordId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
public class FleetDb(DbContextOptions<FleetDb> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Audit> Audits => Set<Audit>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Vehicle>().HasIndex(x => new { x.CompanyId, x.Plate }).IsUnique();
        model.Entity<Driver>().HasIndex(x => new { x.CompanyId, x.Cpf }).IsUnique();
        model.Entity<Account>().HasIndex(x => x.Username).IsUnique();
        model.Entity<Vehicle>().Property(x => x.Odometer).HasPrecision(12, 3);
    }
}
public record LoginRequest(string Username, string Password);
public record VehicleRequest(string Plate, string Category, string Brand, string Model, int Year, decimal Odometer, bool Active, byte[]? Version);
public record DriverRequest(string Name, string Cpf, string License, string LicenseCategory, DateOnly LicenseExpiry, bool Active, byte[]? Version);

public static class IdentityScope
{
    public static int Company(HttpContext context) => int.Parse(context.User.FindFirst("company")!.Value);
}
