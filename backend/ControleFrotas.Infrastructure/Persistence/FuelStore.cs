using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public sealed class FuelStore(FleetDb db) : IFuelStore
{
    public Task<FuelEntry?> Request(int company, Guid requestId, CancellationToken ct) => db.Set<FuelEntry>().AsNoTracking().SingleOrDefaultAsync(e => e.CompanyId == company && e.RequestId == requestId, ct);
    public async Task<FuelPage> List(int company, FuelQuery q, CancellationToken ct)
    {
        var entries = db.Set<FuelEntry>().AsNoTracking().Where(e => e.CompanyId == company);
        if (q.VehicleId > 0) entries = entries.Where(e => e.VehicleId == q.VehicleId);
        if (q.From.HasValue) entries = entries.Where(e => e.Date >= q.From.Value);
        if (q.To.HasValue) entries = entries.Where(e => e.Date <= q.To.Value);
        var count = await entries.CountAsync(ct);
        var totalLiters = await entries.SumAsync(e => (decimal?)e.Liters, ct) ?? 0;
        var totalCost = await entries.SumAsync(e => (decimal?)e.Total, ct) ?? 0;
        var rows = await (from e in entries
                          join v in db.Vehicles on e.VehicleId equals v.Id
                          where v.CompanyId == company
                          orderby e.Date descending, e.Id descending
                          select new FuelRow(e.Id, e.VehicleId, v.Plate, e.Date, e.Odometer, e.FuelType, e.Liters, e.UnitPrice, e.Total, e.Station, e.Reference, e.FullTank, e.KmPerLiter)).Skip((q.Page - 1) * 20).Take(20).ToListAsync(ct);
        return new FuelPage(rows, count, totalLiters, totalCost);
    }
    public async Task<FuelWindow> Window(int company, int vehicle, CancellationToken ct)
    {
        var query = db.Set<FuelEntry>().AsNoTracking().Where(e => e.CompanyId == company && e.VehicleId == vehicle);
        var last = await query.OrderByDescending(e => e.Id).Select(e => (DateOnly?)e.Date).FirstOrDefaultAsync(ct);
        var full = await query.Where(e => e.FullTank).OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        var liters = full is null ? 0 : await query.Where(e => e.Id > full.Id).SumAsync(e => e.Liters, ct);
        return new FuelWindow(last, full?.Odometer, liters);
    }
    public async Task Save(FuelEntry entry, Vehicle vehicle, byte[] version, Audit audit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Entry(vehicle).Property(v => v.Version).OriginalValue = version;
        db.Entry(vehicle).Property(v => v.Odometer).IsModified = true;
        db.Set<FuelEntry>().Add(entry);
        await db.SaveChangesAsync(ct);
        audit.RecordId = entry.Id; db.Audits.Add(audit);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
