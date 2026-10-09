using ControleFrotas;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;

public static class SqlIntegration
{
    public static async Task<int> Run()
    {
        var connection = Environment.GetEnvironmentVariable("FLEET_TEST_SQL");
        if (string.IsNullOrWhiteSpace(connection))
        {
            Console.WriteLine("Integração SQL não executada: FLEET_TEST_SQL não configurada.");
            return 0;
        }
        var master = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        await WaitForServer(master.ConnectionString);
        var databases = new List<string>();
        var checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
        async Task<string> CreateDatabase()
        {
            var name = "Fleet_CI_" + Guid.NewGuid().ToString("N");
            await using var sql = new SqlConnection(master.ConnectionString);
            await sql.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", sql);
            await command.ExecuteNonQueryAsync();
            databases.Add(name);
            return new SqlConnectionStringBuilder(connection) { InitialCatalog = name }.ConnectionString;
        }
        FleetDb Context(string cs) => new(new DbContextOptionsBuilder<FleetDb>().UseSqlServer(cs).Options);
        try
        {
            var freshConnection = await CreateDatabase();
            await using (var fresh = Context(freshConnection))
            {
                await fresh.Database.MigrateAsync();
                Check((await fresh.Database.GetAppliedMigrationsAsync()).Count() == 3, "Banco novo recebe três migrations");
                Check(await fresh.FuelEntries.CountAsync() == 0, "Tabela de abastecimentos criada no banco novo");
            }
            var legacyConnection = await CreateDatabase();
            int vehicleId;
            await using (var legacy = Context(legacyConnection))
            {
                var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "schema-v0.1.sql"));
                await legacy.Database.ExecuteSqlRawAsync(Regex.Replace(schema, @"^GO\s*$", "", RegexOptions.Multiline));
                // Seed using the old table shape; new EF model expects catalog columns.
                await legacy.Database.ExecuteSqlRawAsync("INSERT INTO Vehicles (CompanyId,Plate,Category,Brand,Model,Year,Odometer,Active) VALUES (1,'ABC1D23','Carro','Marca','Modelo',2024,1000,1)");
                legacy.Accounts.Add(new Account { Username = "baseline-admin", PasswordHash = "hash-preservado-de-teste" });
                legacy.Drivers.Add(new Driver { CompanyId = 1, Name = "Motorista teste", Cpf = "52998224725", License = "12345678901", LicenseExpiry = new DateOnly(2030, 1, 1) });
                legacy.Audits.Add(new Audit { CompanyId = 1, Actor = "baseline-admin", Action = "Antes da atualização" });
                await legacy.SaveChangesAsync();
                vehicleId = await legacy.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM Vehicles WHERE Plate = 'ABC1D23'").SingleAsync();
            }
            await using (var upgraded = Context(legacyConnection))
            {
                await upgraded.Database.MigrateAsync();
                var migratedVehicle = await upgraded.Vehicles.SingleAsync();
                Check(migratedVehicle.BrandId > 0 && migratedVehicle.ModelId > 0 && migratedVehicle.Brand == "Marca" && migratedVehicle.Model == "Modelo", "Migração preserva rótulos e cria vínculos do veículo legado");
                Check(await upgraded.VehicleBrands.CountAsync() == 1 && await upgraded.VehicleModels.CountAsync() == 1, "Catálogo criado a partir dos veículos existentes");
                Check(await upgraded.Accounts.CountAsync() == 1 && (await upgraded.Accounts.SingleAsync()).PasswordHash == "hash-preservado-de-teste", "Preserva administrador e hash no banco legado");
                Check(await upgraded.Vehicles.CountAsync() == 1 && await upgraded.Drivers.CountAsync() == 1, "Preserva veículos e motoristas no banco legado");
                Check(await upgraded.Audits.CountAsync() == 1, "Preserva auditoria anterior");
                await upgraded.Database.MigrateAsync();
                Check(await upgraded.Accounts.CountAsync() == 1 && await upgraded.FuelEntries.CountAsync() == 0, "Reiniciar migrations não recria cadastros");
            }
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
            FuelRequest request;
            await using (var operation = Context(legacyConnection))
            {
                var vehicle = await operation.Vehicles.SingleAsync();
                request = new FuelRequest(vehicleId, Guid.NewGuid(), today.AddDays(-3), 1000, "Gasolina", 40, 6.1234m, "Posto", "NF-01", true, vehicle.Version);
                var service = new FuelService(new FuelStore(operation), new FleetStore(operation), TimeProvider.System);
                await service.Register(1, "baseline-admin", request, CancellationToken.None);
                await service.Register(1, "baseline-admin", request, CancellationToken.None);
            }
            await using (var check = Context(legacyConnection))
            {
                Check(await check.FuelEntries.CountAsync() == 1 && await check.Audits.CountAsync() == 2, "Reenvio não duplica abastecimento nem auditoria");
                Check((await check.FuelEntries.SingleAsync()).Total == 244.94m, "Total monetário persistido corretamente");
            }
            byte[] staleVersion;
            await using (var partial = Context(legacyConnection))
            {
                var vehicle = await partial.Vehicles.SingleAsync(); staleVersion = vehicle.Version;
                var service = new FuelService(new FuelStore(partial), new FleetStore(partial), TimeProvider.System);
                await service.Register(1, "baseline-admin", request with { RequestId = Guid.NewGuid(), Date = today.AddDays(-2), Odometer = 1200, Liters = 20, FullTank = false, VehicleVersion = vehicle.Version }, CancellationToken.None);
            }
            await using (var stale = Context(legacyConnection))
            {
                var service = new FuelService(new FuelStore(stale), new FleetStore(stale), TimeProvider.System);
                try
                {
                    await service.Register(1, "baseline-admin", request with { RequestId = Guid.NewGuid(), Date = today.AddDays(-1), Odometer = 1500, VehicleVersion = staleVersion }, CancellationToken.None);
                    throw new Exception("Gravação com versão desatualizada deveria falhar.");
                }
                catch (DbUpdateConcurrencyException) { checks++; }
            }
            await using (var check = Context(legacyConnection))
            {
                Check(await check.FuelEntries.CountAsync() == 2 && await check.Audits.CountAsync() == 3 && (await check.Vehicles.SingleAsync()).Odometer == 1200, "Conflito reverte abastecimento, hodômetro e auditoria");
                var vehicle = await check.Vehicles.SingleAsync();
                var service = new FuelService(new FuelStore(check), new FleetStore(check), TimeProvider.System);
                var full = await service.Register(1, "baseline-admin", request with { RequestId = Guid.NewGuid(), Date = today.AddDays(-1), Odometer = 1500, Liters = 30, VehicleVersion = vehicle.Version }, CancellationToken.None);
                Check(full.KmPerLiter == 10m && (await check.Vehicles.SingleAsync()).Odometer == 1500, "Consumo e hodômetro persistidos após intervalo completo");
                var page = await service.List(1, new FuelQuery(VehicleId: vehicleId), CancellationToken.None);
                Check(page.TotalCount == 3 && page.TotalLiters == 90 && page.Items.Count == 3, "Filtros, paginação e totais no SQL");
                Check((await service.List(2, new FuelQuery(), CancellationToken.None)).TotalCount == 0, "Listagem SQL isolada por empresa");
            }
            var catalogUpgradeConnection = await CreateDatabase();
            await using (var previous = Context(catalogUpgradeConnection))
            {
                await previous.GetService<IMigrator>().MigrateAsync("20261009124954_AddFuelEntries");
                await previous.Database.ExecuteSqlRawAsync("""
                    INSERT INTO Vehicles (CompanyId,Plate,Category,Brand,Model,Year,Odometer,Active) VALUES
                    (1,'ABC1D23','Carro',' Toyota ','Corolla ',2024,1000,1),
                    (1,'DEF1D23','Carro','TOYOTA','COROLLA',2024,1000,1),
                    (2,'ABC1D23','Carro','Toyota','Corolla',2024,1000,1);
                    INSERT INTO FuelEntries (CompanyId,VehicleId,RequestId,Date,Odometer,FuelType,Liters,UnitPrice,Total,Station,Reference,FullTank)
                    SELECT 1,Id,NEWID(),'2026-01-01',1000,'Gasolina',40,6,240,'Posto','NF-TESTE',1 FROM Vehicles WHERE CompanyId=1 AND Plate='ABC1D23';
                    """);
            }
            await using (var migrated = Context(catalogUpgradeConnection))
            {
                await migrated.Database.MigrateAsync();
                Check(await migrated.VehicleBrands.CountAsync() == 2 && await migrated.VehicleModels.CountAsync() == 2, "Migração agrupa nomes por empresa ignorando maiúsculas e espaços");
                var vehicles = await migrated.Vehicles.OrderBy(v => v.CompanyId).ThenBy(v => v.Plate).ToListAsync();
                Check(vehicles[0].BrandId == vehicles[1].BrandId && vehicles[0].ModelId == vehicles[1].ModelId && vehicles[0].BrandId != vehicles[2].BrandId, "Migração separa empresas e reutiliza vínculos equivalentes");
                Check(vehicles[0].Brand == " Toyota " && vehicles[0].Model == "Corolla " && (await migrated.FuelEntries.SingleAsync()).Total == 240, "Atualização do catálogo preserva rótulos originais e abastecimento anterior");
                await migrated.Database.MigrateAsync();
                Check(await migrated.VehicleModels.CountAsync() == 2 && await migrated.FuelEntries.CountAsync() == 1, "Reiniciar atualização não duplica o catálogo");
            }
            checks += await CatalogIntegration.Run(freshConnection);
            var invalidConnection = await CreateDatabase();
            await using (var invalid = Context(invalidConnection))
            {
                await invalid.Database.ExecuteSqlRawAsync("CREATE TABLE Accounts (Id int NOT NULL PRIMARY KEY)");
                try { await invalid.Database.MigrateAsync(); throw new Exception("Banco incompatível deveria ser rejeitado."); }
                catch (SqlException ex) when (ex.Number == 51000) { checks++; }
                Check(await invalid.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'FuelEntries'").SingleAsync() == 0, "Esquema incompatível não recebe tabela operacional");
            }
            Console.WriteLine($"{checks} verificações de integração SQL passaram.");
            return checks;
        }
        finally
        {
            foreach (var database in databases)
            {
                SqlConnection.ClearAllPools();
                await using var sql = new SqlConnection(master.ConnectionString); await sql.OpenAsync();
                await using var command = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]", sql);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
    private static async Task WaitForServer(string cs)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try { await using var sql = new SqlConnection(cs); await sql.OpenAsync(); return; }
            catch (SqlException) when (attempt < 59) { await Task.Delay(TimeSpan.FromSeconds(2)); }
        }
    }
}
