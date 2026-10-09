using Microsoft.EntityFrameworkCore;
namespace ControleFrotas;

public sealed class CatalogStore(FleetDb db) : ICatalogStore
{
    public Task<List<VehicleBrand>> Brands(int company, bool includeInactive, CancellationToken ct) => db.VehicleBrands.AsNoTracking().Where(x => x.CompanyId == company && (includeInactive || x.Active)).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<List<VehicleModel>> Models(int company, int brandId, bool includeInactive, CancellationToken ct) => db.VehicleModels.AsNoTracking().Where(x => x.CompanyId == company && x.BrandId == brandId && (includeInactive || x.Active && db.VehicleBrands.Any(b => b.Id == x.BrandId && b.CompanyId == company && b.Active))).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<VehicleBrand?> Brand(int company, int id, CancellationToken ct) => db.VehicleBrands.SingleOrDefaultAsync(x => x.CompanyId == company && x.Id == id, ct);
    public Task<VehicleModel?> Model(int company, int id, CancellationToken ct) => db.VehicleModels.SingleOrDefaultAsync(x => x.CompanyId == company && x.Id == id, ct);
    public async Task SaveBrand(VehicleBrand brand, byte[]? version, Audit audit, bool create, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Prepare(brand, version, create);
        await db.SaveChangesAsync(ct);
        await db.Vehicles.Where(v => v.CompanyId == brand.CompanyId && v.BrandId == brand.Id && v.Brand != brand.Name).ExecuteUpdateAsync(s => s.SetProperty(v => v.Brand, brand.Name), ct);
        await Audit(brand.Id, audit, ct);
        await transaction.CommitAsync(ct);
    }
    public async Task SaveModel(VehicleModel model, byte[]? version, Audit audit, bool create, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Prepare(model, version, create);
        await db.SaveChangesAsync(ct);
        await db.Vehicles.Where(v => v.CompanyId == model.CompanyId && v.ModelId == model.Id && v.Model != model.Name).ExecuteUpdateAsync(s => s.SetProperty(v => v.Model, model.Name), ct);
        await Audit(model.Id, audit, ct);
        await transaction.CommitAsync(ct);
    }
    private void Prepare<T>(T entity, byte[]? version, bool create) where T : class
    {
        if (create) db.Add(entity);
        else db.Entry(entity).Property("Version").OriginalValue = version;
    }
    private async Task Audit(int id, Audit audit, CancellationToken ct)
    {
        audit.RecordId = id; db.Audits.Add(audit); await db.SaveChangesAsync(ct);
    }
}
