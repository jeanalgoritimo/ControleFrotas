namespace ControleFrotas;

public class Account
{
    public int Id { get; set; }
    public int CompanyId { get; set; } = 1;
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
