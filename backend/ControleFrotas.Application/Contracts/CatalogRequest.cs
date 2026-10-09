namespace ControleFrotas;

public record BrandRequest(string Name, bool Active, byte[]? Version);
public record ModelRequest(int BrandId, string Name, bool Active, byte[]? Version);
