namespace ControleFrotas;

public interface ICatalogStore
{
    Task<List<VehicleBrand>> Brands(int company, bool includeInactive, CancellationToken ct);
    Task<List<VehicleModel>> Models(int company, int brandId, bool includeInactive, CancellationToken ct);
    Task<VehicleBrand?> Brand(int company, int id, CancellationToken ct);
    Task<VehicleModel?> Model(int company, int id, CancellationToken ct);
    Task SaveBrand(VehicleBrand brand, byte[]? version, Audit audit, bool create, CancellationToken ct);
    Task SaveModel(VehicleModel model, byte[]? version, Audit audit, bool create, CancellationToken ct);
}
