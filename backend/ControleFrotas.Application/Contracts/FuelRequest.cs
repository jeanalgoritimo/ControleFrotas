namespace ControleFrotas;

public sealed record FuelRequest(int VehicleId, Guid RequestId, DateOnly Date, decimal Odometer, string FuelType, decimal Liters, decimal UnitPrice, string Station, string? Reference, bool FullTank, byte[]? VehicleVersion);
