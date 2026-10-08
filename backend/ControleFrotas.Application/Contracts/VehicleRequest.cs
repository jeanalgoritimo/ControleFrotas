namespace ControleFrotas;

public record VehicleRequest(string Plate, string Category, string Brand, string Model, int Year, decimal Odometer, bool Active, byte[]? Version);
