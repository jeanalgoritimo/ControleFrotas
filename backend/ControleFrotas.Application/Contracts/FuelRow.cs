namespace ControleFrotas;

public sealed record FuelRow(int Id, int VehicleId, string Plate, DateOnly Date, decimal Odometer, string FuelType, decimal Liters, decimal UnitPrice, decimal Total, string Station, string Reference, bool FullTank, decimal? KmPerLiter);
