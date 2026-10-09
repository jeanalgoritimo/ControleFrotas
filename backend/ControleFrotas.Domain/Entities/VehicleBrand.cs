namespace ControleFrotas;

public class VehicleBrand
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool Active { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
