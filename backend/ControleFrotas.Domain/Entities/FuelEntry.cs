namespace ControleFrotas;

public sealed class FuelEntry
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int VehicleId { get; set; }
    public Guid RequestId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Odometer { get; set; }
    public string FuelType { get; set; } = "";
    public decimal Liters { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public string Station { get; set; } = "";
    public string Reference { get; set; } = "";
    public bool FullTank { get; set; }
    public decimal? KmPerLiter { get; set; }
}
