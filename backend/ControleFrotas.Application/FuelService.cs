namespace ControleFrotas;

public sealed class FuelService(IFuelStore store, IFleetStore fleet, TimeProvider clock)
{
    public Task<FuelPage> List(int company, FuelQuery q, CancellationToken ct)
    {
        if (q.Page < 1 || q.Page > 100000 || q.VehicleId < 0) throw new BusinessException("Filtro ou página inválida.");
        if (q.From.HasValue && q.To.HasValue && q.From > q.To) throw new BusinessException("A data inicial deve ser anterior ou igual à final.");
        return store.List(company, q, ct);
    }
    public async Task<FuelEntry> Register(int company, string actor, FuelRequest r, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        Validate(r, today);
        var existing = await store.Request(company, r.RequestId, ct);
        if (existing is not null)
        {
            if (existing.VehicleId != r.VehicleId || existing.Date != r.Date || existing.Odometer != r.Odometer || existing.FuelType != r.FuelType || existing.Liters != r.Liters || existing.UnitPrice != r.UnitPrice || existing.Station != r.Station.Trim() || existing.Reference != (r.Reference ?? "").Trim() || existing.FullTank != r.FullTank)
                throw new BusinessException("Esta solicitação já foi registrada com outros dados. Reabra o formulário para um novo lançamento.");
            return existing;
        }
        var vehicle = await fleet.Vehicle(company, r.VehicleId, ct) ?? throw new RecordNotFoundException();
        if (!vehicle.Active) throw new BusinessException("O veículo está inativo.");
        if (r.Odometer < vehicle.Odometer) throw new BusinessException("O hodômetro não pode ser menor que o atual do veículo.");
        var window = await store.Window(company, vehicle.Id, ct);
        if (window.LastDate.HasValue && r.Date < window.LastDate) throw new BusinessException("Registre o abastecimento na mesma data ou após o último lançamento do veículo.");
        var entry = new FuelEntry
        {
            CompanyId = company,
            VehicleId = vehicle.Id,
            RequestId = r.RequestId,
            Date = r.Date,
            Odometer = r.Odometer,
            FuelType = r.FuelType,
            Liters = r.Liters,
            UnitPrice = r.UnitPrice,
            Total = FuelMath.Total(r.Liters, r.UnitPrice),
            Station = r.Station.Trim(),
            Reference = (r.Reference ?? "").Trim(),
            FullTank = r.FullTank,
            KmPerLiter = FuelMath.Consumption(r.FullTank, r.Odometer, window.FullOdometer, window.LitersSinceFull + r.Liters)
        };
        vehicle.Odometer = r.Odometer;
        await store.Save(entry, vehicle, r.VehicleVersion!, new Audit { CompanyId = company, Actor = actor, Action = "Abastecimento registrado" }, ct);
        return entry;
    }
    public static void Validate(FuelRequest r, DateOnly today)
    {
        if (r.VehicleId <= 0 || r.RequestId == Guid.Empty) throw new BusinessException("Selecione o veículo e uma solicitação válida.");
        if (r.VehicleVersion is not { Length: 8 }) throw new BusinessException("Atualize o cadastro do veículo antes de registrar o abastecimento.");
        if (r.Date < new DateOnly(1900, 1, 1) || r.Date > today) throw new BusinessException("Informe uma data válida, sem data futura.");
        if (r.Odometer < 0 || r.Odometer > 999999999 || decimal.Round(r.Odometer, 3) != r.Odometer) throw new BusinessException("Hodômetro inválido: até três casas decimais.");
        if (r.Liters <= 0 || r.Liters > 9999.999m || decimal.Round(r.Liters, 3) != r.Liters) throw new BusinessException("Volume deve ser maior que zero, com até três casas decimais.");
        if (r.UnitPrice <= 0 || r.UnitPrice > 9999.9999m || decimal.Round(r.UnitPrice, 4) != r.UnitPrice) throw new BusinessException("Preço por litro deve ser maior que zero, com até quatro casas decimais.");
        if (r.FuelType is not ("Gasolina" or "Etanol" or "Diesel")) throw new BusinessException("Combustível inválido.");
        if (string.IsNullOrWhiteSpace(r.Station) || r.Station.Trim().Length > 150 || (r.Reference?.Trim().Length ?? 0) > 80) throw new BusinessException("Informe o posto com até 150 caracteres e a referência com até 80.");
    }
}
