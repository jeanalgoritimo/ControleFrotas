using ControleFrotas;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
int checks = 0;
void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
Check(Validation.Plate(" abc-1234 ") == "ABC1234", "Normalização de placa");
Check(Validation.ValidPlate("ABC1234"), "Placa antiga");
Check(Validation.ValidPlate("ABC1D23"), "Placa Mercosul");
Check(!Validation.ValidPlate("ABC12345"), "Placa longa");
Check(!Validation.ValidPlate("123ABCD"), "Placa com ordem inválida");
Check(Validation.ValidCpf("52998224725"), "CPF com dígitos verificadores válidos");
Check(!Validation.ValidCpf("52998224724"), "CPF com dígito inválido");
Check(!Validation.ValidCpf("11111111111"), "CPF repetido");
var vehicle = new VehicleRequest("ABC1D23", "Carro", "Marca", "Modelo", 2024, 12000.125m, true, null);
Check(Validation.VehicleError(vehicle) is null, "Veículo válido");
Check(Validation.VehicleError(vehicle with { Odometer = -1 }) is not null, "Hodômetro negativo");
Check(Validation.VehicleError(vehicle with { Odometer = 1.0001m }) is not null, "Precisão do hodômetro");
Check(Validation.VehicleError(vehicle with { Category = "Inválida" }) is not null, "Categoria inexistente");
Check(Validation.VehicleError(vehicle with { Brand = " " }) is not null, "Marca vazia");
var driver = new DriverRequest("Motorista de teste", "52998224725", "12345678901", "B", new DateOnly(2030, 1, 1), true, null);
Check(Validation.DriverError(driver) is null, "Motorista com formato válido");
Check(Validation.DriverError(driver with { License = "abc" }) is not null, "Formato CNH inválido");
Check(Validation.DriverError(driver with { LicenseCategory = "Z" }) is not null, "Categoria CNH inválida");
var options = new DbContextOptionsBuilder<FleetDb>().UseSqlServer("Server=localhost;Database=ModelCheck;Integrated Security=True;TrustServerCertificate=True").Options;
using var db = new FleetDb(options);
var script = db.Database.GenerateCreateScript();
Check(script.Contains("rowversion"), "Concorrência no esquema SQL");
Check(script.Contains("decimal(12,3)"), "Precisão no esquema SQL");
var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("company", "1") }, "test")) };
var query = db.Vehicles.Where(x => x.CompanyId == IdentityScope.Company(context)).ToQueryString();
Check(query.Contains("WHERE") && query.Contains("CompanyId"), "Filtro de empresa traduzido para SQL");

// Application tests use a store double: no SQL Server required.
var store = new TestFleetStore();
var service = new FleetService(store);
var created = await service.SaveVehicle(2, "admin", null, vehicle, CancellationToken.None);
Check(created.CompanyId == 2 && store.LastAudit?.CompanyId == 2, "Empresa da sessão aplicada ao cadastro e auditoria");
Check(created.Plate == "ABC1D23" && store.LastAudit?.Actor == "admin", "Normalização e autoria");
async Task Rejected<T>(Func<Task> action, string message) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new Exception(message);
}
await Rejected<BusinessException>(() => service.SaveVehicle(2, "admin", created.Id, vehicle with { Version = new byte[] { 1 }, Odometer = 1 }, CancellationToken.None), "Hodômetro decrescente deve ser rejeitado");
await Rejected<BusinessException>(() => service.SaveVehicle(2, "admin", created.Id, vehicle, CancellationToken.None), "Edição sem versão deve ser rejeitada");
await Rejected<RecordNotFoundException>(() => service.SaveVehicle(1, "admin", created.Id, vehicle with { Version = new byte[] { 1 } }, CancellationToken.None), "Cadastro de outra empresa deve ser invisível");
Check(store.Writes == 1, "Rejeições não persistem alterações");
await service.SaveVehicle(2, "admin", created.Id, vehicle with { Version = new byte[] { 1 }, Active = false }, CancellationToken.None);
Check(!created.Active && store.LastAudit?.Action == "Veículo editado", "Inativação preserva registro e gera auditoria");
await Rejected<BusinessException>(() => service.SaveDriver(2, "admin", null, driver with { Cpf = "11111111111" }, CancellationToken.None), "CPF inválido não persiste");
var createdDriver = await service.SaveDriver(2, "admin", null, driver, CancellationToken.None);
Check(createdDriver.CompanyId == 2, "Empresa aplicada ao motorista");
await Rejected<RecordNotFoundException>(() => service.SaveDriver(1, "admin", createdDriver.Id, driver with { Version = new byte[] { 1 } }, CancellationToken.None), "Motorista de outra empresa deve ser invisível");
Check(!typeof(Vehicle).Assembly.GetReferencedAssemblies().Any(x => x.Name!.Contains("EntityFramework") || x.Name.Contains("AspNetCore")), "Domain independente de EF e ASP.NET");
Check(!typeof(FleetService).Assembly.GetReferencedAssemblies().Any(x => x.Name!.Contains("Infrastructure") || x.Name.Contains("EntityFramework")), "Application independente da persistência");
var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema-v0.1.sql");
Check(script.Replace("\r\n", "\n").TrimEnd() == File.ReadAllText(schemaPath).Replace("\r\n", "\n").TrimEnd(), "Esquema SQL compatível com v0.1");
Console.WriteLine($"{checks} verificações de regras e arquitetura passaram.");

sealed class TestFleetStore : IFleetStore
{
    private readonly List<Vehicle> vehicles = [];
    private readonly List<Driver> drivers = [];
    public Audit? LastAudit { get; private set; }
    public int Writes { get; private set; }
    public Task<List<Vehicle>> Vehicles(int company, CancellationToken ct) => Task.FromResult(vehicles.Where(v => v.CompanyId == company).ToList());
    public Task<List<Driver>> Drivers(int company, CancellationToken ct) => Task.FromResult(drivers.Where(d => d.CompanyId == company).ToList());
    public Task<List<Audit>> Audits(int company, CancellationToken ct) => Task.FromResult(new List<Audit>());
    public Task<Vehicle?> Vehicle(int company, int id, CancellationToken ct) => Task.FromResult(vehicles.SingleOrDefault(v => v.CompanyId == company && v.Id == id));
    public Task<Driver?> Driver(int company, int id, CancellationToken ct) => Task.FromResult(drivers.SingleOrDefault(d => d.CompanyId == company && d.Id == id));
    public Task SaveVehicle(Vehicle v, byte[]? version, Audit audit, bool create, CancellationToken ct)
    {
        if (create) { v.Id = vehicles.Count + 1; vehicles.Add(v); }
        LastAudit = audit; Writes++; return Task.CompletedTask;
    }
    public Task SaveDriver(Driver d, byte[]? version, Audit audit, bool create, CancellationToken ct)
    {
        if (create) { d.Id = drivers.Count + 1; drivers.Add(d); }
        LastAudit = audit; Writes++; return Task.CompletedTask;
    }
}
