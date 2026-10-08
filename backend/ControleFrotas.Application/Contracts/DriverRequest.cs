namespace ControleFrotas;

public record DriverRequest(string Name, string Cpf, string License, string LicenseCategory, DateOnly LicenseExpiry, bool Active, byte[]? Version);
