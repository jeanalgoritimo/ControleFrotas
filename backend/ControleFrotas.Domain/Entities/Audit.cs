namespace ControleFrotas;

public class Audit
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public int RecordId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
