using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public class FleetDb(DbContextOptions<FleetDb> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Audit> Audits => Set<Audit>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Vehicle>().Property(x => x.Plate).HasMaxLength(7);
        model.Entity<Vehicle>().Property(x => x.Category).HasMaxLength(20);
        model.Entity<Vehicle>().Property(x => x.Brand).HasMaxLength(80);
        model.Entity<Vehicle>().Property(x => x.Model).HasMaxLength(100);
        model.Entity<Driver>().Property(x => x.Name).HasMaxLength(150);
        model.Entity<Driver>().Property(x => x.Cpf).HasMaxLength(11);
        model.Entity<Driver>().Property(x => x.License).HasMaxLength(11);
        model.Entity<Driver>().Property(x => x.LicenseCategory).HasMaxLength(2);
        model.Entity<Account>().Property(x => x.Username).HasMaxLength(100);
        model.Entity<Vehicle>().Property(x => x.Version).IsRowVersion();
        model.Entity<Driver>().Property(x => x.Version).IsRowVersion();
        model.Entity<Vehicle>().HasIndex(x => new { x.CompanyId, x.Plate }).IsUnique();
        model.Entity<Driver>().HasIndex(x => new { x.CompanyId, x.Cpf }).IsUnique();
        model.Entity<Account>().HasIndex(x => x.Username).IsUnique();
        model.Entity<Vehicle>().Property(x => x.Odometer).HasPrecision(12, 3);
    }
}
