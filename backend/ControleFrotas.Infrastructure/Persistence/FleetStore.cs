using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public sealed class FleetStore(FleetDb db) : IFleetStore
{
    public Task<List<Vehicle>> Vehicles(int company, CancellationToken ct) => db.Vehicles.AsNoTracking().Where(x => x.CompanyId == company).OrderBy(x => x.Plate).ToListAsync(ct);
    public Task<List<Driver>> Drivers(int company, CancellationToken ct) => db.Drivers.AsNoTracking().Where(x => x.CompanyId == company).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<List<Audit>> Audits(int company, CancellationToken ct) => db.Audits.AsNoTracking().Where(x => x.CompanyId == company).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
    public Task<Vehicle?> Vehicle(int company, int id, CancellationToken ct) => db.Vehicles.SingleOrDefaultAsync(x => x.CompanyId == company && x.Id == id, ct);
    public Task<Driver?> Driver(int company, int id, CancellationToken ct) => db.Drivers.SingleOrDefaultAsync(x => x.CompanyId == company && x.Id == id, ct);
    public Task SaveVehicle(Vehicle v, byte[]? version, Audit audit, bool create, CancellationToken ct) => Save(v, version, audit, create, ct);
    public Task SaveDriver(Driver d, byte[]? version, Audit audit, bool create, CancellationToken ct) => Save(d, version, audit, create, ct);
    private async Task Save<T>(T entity, byte[]? version, Audit audit, bool create, CancellationToken ct) where T : class
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (create) db.Add(entity);
        else db.Entry(entity).Property("Version").OriginalValue = version;
        await db.SaveChangesAsync(ct);
        audit.RecordId = (int)db.Entry(entity).Property("Id").CurrentValue!;
        db.Audits.Add(audit);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
