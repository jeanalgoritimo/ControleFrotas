namespace ControleFrotas;

public class Vehicle
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Plate { get; set; } = "";
    public string Category { get; set; } = "Carro";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public int Year { get; set; }
    public decimal Odometer { get; set; }
    public bool Active { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
