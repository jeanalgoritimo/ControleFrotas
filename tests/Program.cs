using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
var vehicle = new VehicleRequest("ABC1D23", "Carro", "Marca", "Modelo", 2024, 12000.125m, true, null, 1, 1);
Check(Validation.VehicleError(vehicle) is null, "Veículo válido");
Check(Validation.VehicleError(vehicle with { Odometer = -1 }) is not null, "Hodômetro negativo");
Check(Validation.VehicleError(vehicle with { Odometer = 1.0001m }) is not null, "Precisão do hodômetro");
Check(Validation.VehicleError(vehicle with { Category = "Inválida" }) is not null, "Categoria inexistente");
Check(Validation.VehicleError(vehicle with { Brand = " " }) is not null, "Marca vazia");
foreach (var category in new[] { "Carro", "Moto", "Van", "Utilitário" })
{
    Check(Validation.VehicleError(vehicle with { Category = category, Plate = " abc-1234 " }) is null, $"Placa antiga aceita para {category}");
    Check(Validation.VehicleError(vehicle with { Category = category, Plate = " abc1d23 " }) is null, $"Placa Mercosul aceita para {category}");
}
foreach (var invalidPlate in new[] { "ABC12345", "ABC12D3", "AB11234", "ABC!1234", "A-BC1234", "ABC--1234", "ABC 1234", "ABC1234\nX", "ÁBC1234" })
    Check(Validation.VehicleError(vehicle with { Plate = invalidPlate }) is not null, "Placa inválida não é transformada em outra placa válida");
Check(Validation.Plate("abc-1234") == "ABC1234" && Validation.Plate("abc1d23") == "ABC1D23", "Persistência sem máscara não converte o padrão antigo");
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
var catalog = new TestCatalogStore();
var service = new FleetService(store, catalog);
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
// Legacy columns remain unchanged; additional catalog columns are additive.
var original = db.Model.FindEntityType(typeof(Vehicle))!;
Check(original.FindProperty("Brand")!.GetMaxLength() == 80 && original.FindProperty("Model")!.GetMaxLength() == 100 && original.FindProperty("Plate")!.GetMaxLength() == 7, "Tamanhos originais preservados no modelo");

Check(FuelMath.Total(40m, 6.1234m) == 244.94m, "Total arredondado no servidor");
Check(FuelMath.Total(10m, 5.5555m) == 55.56m, "Arredondamento de meio centavo");
Check(FuelMath.Consumption(true, 1500, 1000, 50) == 10m, "Consumo tanque cheio incluindo parciais");
Check(FuelMath.Consumption(false, 1500, 1000, 50) is null, "Parcial sem consumo conclusivo");
Check(FuelMath.Consumption(true, 1000, null, 40) is null, "Primeiro tanque cheio cria referência");
Check(FuelMath.Consumption(true, 1000, 1000, 40) is null, "Distância zero sem consumo");
var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
var fuelFleet = new TestFleetStore();
var fuelVehicle = await new FleetService(fuelFleet, catalog).SaveVehicle(2, "admin", null, vehicle with { Odometer = 1000 }, CancellationToken.None);
var fuelStore = new TestFuelStore();
var fuelService = new FuelService(fuelStore, fuelFleet, TimeProvider.System);
var fuel = new FuelRequest(fuelVehicle.Id, Guid.NewGuid(), today.AddDays(-3), 1000, "Gasolina", 40, 6.1234m, " Posto teste ", " NF-01 ", true, new byte[8]);
var first = await fuelService.Register(2, "admin", fuel, CancellationToken.None);
Check(first.CompanyId == 2 && first.Total == 244.94m && first.Station == "Posto teste", "Empresa, total e normalização no abastecimento");
Check(first.KmPerLiter is null && fuelStore.LastAudit?.Actor == "admin", "Referência inicial e autoria da auditoria");
var repeat = await fuelService.Register(2, "admin", fuel, CancellationToken.None);
Check(repeat.Id == first.Id && fuelStore.Writes == 1, "Reenvio idempotente sem duplicar lançamento");
await Rejected<BusinessException>(() => fuelService.Register(2, "admin", fuel with { Liters = 41 }, CancellationToken.None), "Mesma solicitação com outros dados deve ser rejeitada");
await Rejected<RecordNotFoundException>(() => fuelService.Register(1, "admin", fuel with { RequestId = Guid.NewGuid() }, CancellationToken.None), "Veículo de outra empresa bloqueado no abastecimento");
await Rejected<BusinessException>(() => fuelService.Register(2, "admin", fuel with { RequestId = Guid.NewGuid(), Odometer = 999 }, CancellationToken.None), "Hodômetro regressivo bloqueado");
await Rejected<BusinessException>(() => fuelService.Register(2, "admin", fuel with { RequestId = Guid.NewGuid(), Date = today.AddDays(-4) }, CancellationToken.None), "Data anterior ao último abastecimento bloqueada");
fuelVehicle.Active = false;
await Rejected<BusinessException>(() => fuelService.Register(2, "admin", fuel with { RequestId = Guid.NewGuid() }, CancellationToken.None), "Veículo inativo bloqueado");
fuelVehicle.Active = true;
var partial = await fuelService.Register(2, "admin", fuel with { RequestId = Guid.NewGuid(), Date = today.AddDays(-2), Odometer = 1200, Liters = 20, FullTank = false }, CancellationToken.None);
var full = await fuelService.Register(2, "admin", fuel with { RequestId = Guid.NewGuid(), Date = today.AddDays(-1), Odometer = 1500, Liters = 30 }, CancellationToken.None);
Check(partial.KmPerLiter is null && full.KmPerLiter == 10m, "Intervalo completo usa abastecimento parcial");
Check(fuelVehicle.Odometer == 1500 && fuelStore.LastAudit?.CompanyId == 2, "Atualiza hodômetro e escopo da auditoria");
foreach (var invalid in new[] {
    fuel with { Liters = 0 }, fuel with { Liters = -1 }, fuel with { Liters = 1.0001m },
    fuel with { UnitPrice = 0 }, fuel with { UnitPrice = 1.00001m }, fuel with { UnitPrice = 10000 },
    fuel with { Date = today.AddDays(1) }, fuel with { Odometer = -1 }, fuel with { Odometer = 1.0001m },
    fuel with { FuelType = "GNV" }, fuel with { Station = " " }, fuel with { Station = new string('x',151) },
    fuel with { Reference = new string('x',81) }, fuel with { RequestId = Guid.Empty }, fuel with { VehicleVersion = null }
}) await Rejected<BusinessException>(() => fuelService.Register(2, "admin", invalid, CancellationToken.None), "Dados inválidos de abastecimento rejeitados");
await Rejected<BusinessException>(() => fuelService.List(2, new FuelQuery(Page: 0), CancellationToken.None), "Página inválida bloqueada");
await Rejected<BusinessException>(() => fuelService.List(2, new FuelQuery(From: today, To: today.AddDays(-1)), CancellationToken.None), "Período invertido bloqueado");
Check(fuelStore.Writes == 3, "Rejeições não geram lançamentos adicionais");
var fuelTable = db.Model.FindEntityType(typeof(FuelEntry))!;
Check(fuelTable.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "RequestId" })), "Índice de idempotência no banco");
Check(fuelTable.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.Restrict, "Histórico de abastecimento protege veículo contra exclusão");
var migrationScript = db.GetService<IMigrator>().GenerateScript();
Check(migrationScript.Contains("Banco incompatível") && migrationScript.Contains("c.is_identity"), "Baseline valida esquema existente");
Check(migrationScript.Contains("CREATE TABLE [FuelEntries]") && !migrationScript.Contains("DROP TABLE"), "Atualização aditiva sem apagar tabelas");
Check(db.Database.GetMigrations().Count() == 3 && !db.Database.HasPendingModelChanges(), "Três migrations e snapshot sincronizado");
var catalogService = new CatalogService(catalog);
var savedBrand = await catalogService.SaveBrand(2, "admin", null, new BrandRequest(" Toyota ", true, null), CancellationToken.None);
Check(savedBrand.Name == "Toyota" && savedBrand.NormalizedName == "TOYOTA", "Normaliza nome e chave da marca");
var savedModel = await catalogService.SaveModel(2, "admin", null, new ModelRequest(savedBrand.Id, " Corolla ", true, null), CancellationToken.None);
Check(savedModel.BrandId == savedBrand.Id && savedModel.Name == "Corolla" && catalog.LastAudit?.CompanyId == 2, "Modelo vinculado e auditado na empresa");
await Rejected<BusinessException>(() => service.SaveVehicle(2, "admin", null, vehicle with { BrandId = savedBrand.Id }, CancellationToken.None), "Modelo de outra marca bloqueado");
await Rejected<BusinessException>(() => service.SaveVehicle(1, "admin", null, vehicle, CancellationToken.None), "Catálogo de outra empresa bloqueado");
await Rejected<BusinessException>(() => service.SaveVehicle(2, "admin", null, vehicle with { BrandId = 0, ModelId = 0 }, CancellationToken.None), "IDs obrigatórios mesmo enviando nomes");
await Rejected<BusinessException>(() => catalogService.SaveBrand(2, "admin", null, new BrandRequest(" ", true, null), CancellationToken.None), "Marca vazia bloqueada");
await Rejected<BusinessException>(() => catalogService.SaveModel(2, "admin", null, new ModelRequest(999, "Corolla", true, null), CancellationToken.None), "Modelo sem marca cadastrada bloqueado");
await Rejected<BusinessException>(() => catalogService.SaveModel(2, "admin", savedModel.Id, new ModelRequest(1, "Corolla", true, new byte[8]), CancellationToken.None), "Modelo não muda de marca");
await Rejected<BusinessException>(() => catalogService.SaveBrand(2, "admin", savedBrand.Id, new BrandRequest("Toyota", true, null), CancellationToken.None), "Marca exige versão para editar");
await Rejected<RecordNotFoundException>(() => catalogService.SaveBrand(1, "admin", savedBrand.Id, new BrandRequest("Toyota", true, new byte[8]), CancellationToken.None), "Edição de catálogo isolada por empresa");
savedBrand.Active = false;
await Rejected<BusinessException>(() => catalogService.SaveModel(2, "admin", null, new ModelRequest(savedBrand.Id, "Etios", true, null), CancellationToken.None), "Marca inativa impede novo modelo");
catalog.BrandsData[0].Active = false;
await Rejected<BusinessException>(() => service.SaveVehicle(2, "admin", null, vehicle, CancellationToken.None), "Marca inativa impede novo veículo");
await service.SaveVehicle(2, "admin", created.Id, vehicle with { Version = new byte[8], Odometer = created.Odometer }, CancellationToken.None);
Check(created.BrandId == 1 && created.ModelId == 1, "Edição preserva associação inativa existente");
catalog.BrandsData[0].Active = true; catalog.ModelsData[0].Active = false;
await Rejected<BusinessException>(() => service.SaveVehicle(2, "admin", null, vehicle, CancellationToken.None), "Modelo inativo impede novo veículo");
catalog.ModelsData[0].Active = true;
var canonical = await service.SaveVehicle(2, "admin", null, vehicle with { Plate = "DEF1D23", Brand = "Falsa", Model = "Falso" }, CancellationToken.None);
Check(canonical.Brand == "Marca" && canonical.Model == "Modelo", "Servidor usa nomes do catálogo e ignora rótulos enviados");
var fk = original.GetForeignKeys().Single();
Check(fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "BrandId", "ModelId" }), "Banco impõe marca, modelo e empresa no vínculo do veículo");
checks += await SqlIntegration.Run();
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

sealed class TestFuelStore : IFuelStore
{
    public List<FuelEntry> Entries { get; } = [];
    public Audit? LastAudit { get; private set; }
    public int Writes { get; private set; }
    public Task<FuelEntry?> Request(int company, Guid requestId, CancellationToken ct) => Task.FromResult(Entries.SingleOrDefault(e => e.CompanyId == company && e.RequestId == requestId));
    public Task<FuelPage> List(int company, FuelQuery query, CancellationToken ct) => Task.FromResult(new FuelPage([], 0, 0, 0));
    public Task<FuelWindow> Window(int company, int vehicle, CancellationToken ct)
    {
        var entries = Entries.Where(e => e.CompanyId == company && e.VehicleId == vehicle).OrderBy(e => e.Id).ToList();
        var full = entries.LastOrDefault(e => e.FullTank);
        return Task.FromResult(new FuelWindow(entries.LastOrDefault()?.Date, full?.Odometer, full is null ? 0 : entries.Where(e => e.Id > full.Id).Sum(e => e.Liters)));
    }
    public Task Save(FuelEntry entry, Vehicle vehicle, byte[] version, Audit audit, CancellationToken ct)
    {
        entry.Id = Entries.Count + 1; Entries.Add(entry); Writes++; LastAudit = audit; return Task.CompletedTask;
    }
}

sealed class TestCatalogStore : ICatalogStore
{
    public List<VehicleBrand> BrandsData { get; } = [new() { Id = 1, CompanyId = 2, Name = "Marca", NormalizedName = "MARCA" }];
    public List<VehicleModel> ModelsData { get; } = [new() { Id = 1, CompanyId = 2, BrandId = 1, Name = "Modelo", NormalizedName = "MODELO" }];
    public Audit? LastAudit { get; private set; }
    public Task<List<VehicleBrand>> Brands(int company, bool includeInactive, CancellationToken ct) => Task.FromResult(BrandsData.Where(b => b.CompanyId == company && (includeInactive || b.Active)).ToList());
    public Task<List<VehicleModel>> Models(int company, int brandId, bool includeInactive, CancellationToken ct) => Task.FromResult(ModelsData.Where(m => m.CompanyId == company && m.BrandId == brandId && (includeInactive || m.Active)).ToList());
    public Task<VehicleBrand?> Brand(int company, int id, CancellationToken ct) => Task.FromResult(BrandsData.SingleOrDefault(b => b.CompanyId == company && b.Id == id));
    public Task<VehicleModel?> Model(int company, int id, CancellationToken ct) => Task.FromResult(ModelsData.SingleOrDefault(m => m.CompanyId == company && m.Id == id));
    public Task SaveBrand(VehicleBrand brand, byte[]? version, Audit audit, bool create, CancellationToken ct)
    {
        if (create) { brand.Id = BrandsData.Count + 1; BrandsData.Add(brand); }
        LastAudit = audit; return Task.CompletedTask;
    }
    public Task SaveModel(VehicleModel model, byte[]? version, Audit audit, bool create, CancellationToken ct)
    {
        if (create) { model.Id = ModelsData.Count + 1; ModelsData.Add(model); }
        LastAudit = audit; return Task.CompletedTask;
    }
}
