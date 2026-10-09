using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public class FleetDb(DbContextOptions<FleetDb> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<FuelEntry> FuelEntries => Set<FuelEntry>();
    public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<VehicleBrand>().Property(x => x.Name).HasMaxLength(80);
        model.Entity<VehicleBrand>().Property(x => x.NormalizedName).HasMaxLength(80).UseCollation("Latin1_General_100_BIN2");
        model.Entity<VehicleBrand>().Property(x => x.Version).IsRowVersion();
        model.Entity<VehicleBrand>().HasIndex(x => new { x.CompanyId, x.NormalizedName }).IsUnique();
        model.Entity<VehicleBrand>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        model.Entity<VehicleModel>().Property(x => x.Name).HasMaxLength(100);
        model.Entity<VehicleModel>().Property(x => x.NormalizedName).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        model.Entity<VehicleModel>().Property(x => x.Version).IsRowVersion();
        model.Entity<VehicleModel>().HasIndex(x => new { x.CompanyId, x.BrandId, x.NormalizedName }).IsUnique();
        model.Entity<VehicleModel>().HasAlternateKey(x => new { x.CompanyId, x.BrandId, x.Id });
        model.Entity<VehicleModel>().HasOne<VehicleBrand>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BrandId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Vehicle>().HasOne<VehicleModel>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BrandId, x.ModelId }).HasPrincipalKey(x => new { x.CompanyId, x.BrandId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<FuelEntry>().Property(e => e.Odometer).HasPrecision(12, 3);
        model.Entity<FuelEntry>().Property(e => e.Liters).HasPrecision(8, 3);
        model.Entity<FuelEntry>().Property(e => e.UnitPrice).HasPrecision(8, 4);
        model.Entity<FuelEntry>().Property(e => e.Total).HasPrecision(14, 2);
        model.Entity<FuelEntry>().Property(e => e.KmPerLiter).HasPrecision(16, 2);
        model.Entity<FuelEntry>().Property(e => e.FuelType).HasMaxLength(20);
        model.Entity<FuelEntry>().Property(e => e.Station).HasMaxLength(150);
        model.Entity<FuelEntry>().Property(e => e.Reference).HasMaxLength(80);
        model.Entity<FuelEntry>().HasIndex(e => new { e.CompanyId, e.RequestId }).IsUnique();
        model.Entity<FuelEntry>().HasIndex(e => new { e.CompanyId, e.VehicleId, e.Date, e.Id });
        model.Entity<FuelEntry>().HasOne<Vehicle>().WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
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
