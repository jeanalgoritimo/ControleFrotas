namespace ControleFrotas;

public sealed class FleetService(IFleetStore store)
{
    public Task<List<Vehicle>> Vehicles(int company, CancellationToken ct) => store.Vehicles(company, ct);
    public Task<List<Driver>> Drivers(int company, CancellationToken ct) => store.Drivers(company, ct);
    public Task<List<Audit>> Audits(int company, CancellationToken ct) => store.Audits(company, ct);
    public async Task<Vehicle> SaveVehicle(int company, string actor, int? id, VehicleRequest r, CancellationToken ct)
    {
        if (Validation.VehicleError(r) is { } error) throw new BusinessException(error);
        var v = id.HasValue ? await store.Vehicle(company, id.Value, ct) ?? throw new RecordNotFoundException() : new Vehicle { CompanyId = company };
        if (id.HasValue && r.Version is null) throw new BusinessException("Versão do registro obrigatória.");
        if (id.HasValue && r.Odometer < v.Odometer) throw new BusinessException("Hodômetro não pode diminuir nesta edição.");
        v.Plate = Validation.Plate(r.Plate); v.Category = r.Category; v.Brand = r.Brand.Trim(); v.Model = r.Model.Trim(); v.Year = r.Year; v.Odometer = r.Odometer; v.Active = r.Active;
        await store.SaveVehicle(v, r.Version, Log(company, actor, id.HasValue ? "Veículo editado" : "Veículo criado"), !id.HasValue, ct);
        return v;
    }
    public async Task<Driver> SaveDriver(int company, string actor, int? id, DriverRequest r, CancellationToken ct)
    {
        if (Validation.DriverError(r) is { } error) throw new BusinessException(error);
        var d = id.HasValue ? await store.Driver(company, id.Value, ct) ?? throw new RecordNotFoundException() : new Driver { CompanyId = company };
        if (id.HasValue && r.Version is null) throw new BusinessException("Versão do registro obrigatória.");
        d.Name = r.Name.Trim(); d.Cpf = r.Cpf; d.License = r.License; d.LicenseCategory = r.LicenseCategory; d.LicenseExpiry = r.LicenseExpiry; d.Active = r.Active;
        await store.SaveDriver(d, r.Version, Log(company, actor, id.HasValue ? "Motorista editado" : "Motorista criado"), !id.HasValue, ct);
        return d;
    }
    private static Audit Log(int company, string actor, string action) => new() { CompanyId = company, Actor = actor, Action = action };
}
