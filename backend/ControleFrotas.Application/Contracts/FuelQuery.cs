namespace ControleFrotas;

public sealed record FuelQuery(int VehicleId = 0, DateOnly? From = null, DateOnly? To = null, int Page = 1);
