namespace ControleFrotas;

public interface IFuelStore
{
    Task<FuelPage> List(int company, FuelQuery query, CancellationToken ct);
    Task<FuelEntry?> Request(int company, Guid requestId, CancellationToken ct);
    Task<FuelWindow> Window(int company, int vehicle, CancellationToken ct);
    Task Save(FuelEntry entry, Vehicle vehicle, byte[] version, Audit audit, CancellationToken ct);
}
