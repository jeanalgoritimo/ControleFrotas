namespace ControleFrotas;

public interface IFleetStore
{
    Task<List<Vehicle>> Vehicles(int company, CancellationToken ct);
    Task<List<Driver>> Drivers(int company, CancellationToken ct);
    Task<List<Audit>> Audits(int company, CancellationToken ct);
    Task<Vehicle?> Vehicle(int company, int id, CancellationToken ct);
    Task<Driver?> Driver(int company, int id, CancellationToken ct);
    Task SaveVehicle(Vehicle vehicle, byte[]? version, Audit audit, bool create, CancellationToken ct);
    Task SaveDriver(Driver driver, byte[]? version, Audit audit, bool create, CancellationToken ct);
}
