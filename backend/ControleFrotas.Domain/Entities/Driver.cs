namespace ControleFrotas;

public class Driver
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string Cpf { get; set; } = "";
    public string License { get; set; } = "";
    public string LicenseCategory { get; set; } = "B";
    public DateOnly LicenseExpiry { get; set; }
    public bool Active { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
