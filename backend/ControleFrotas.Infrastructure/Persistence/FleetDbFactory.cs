using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ControleFrotas;

public sealed class FleetDbFactory : IDesignTimeDbContextFactory<FleetDb>
{
    public FleetDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<FleetDb>().UseSqlServer("Server=.\\SQLEXPRESS;Database=ControleFrotas;Trusted_Connection=True;TrustServerCertificate=True").Options);
}
