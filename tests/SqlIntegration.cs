using ControleFrotas;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
                Check((await fresh.Database.GetAppliedMigrationsAsync()).Count() == 2, "Banco novo recebe duas migrations");
                Check(await fresh.FuelEntries.CountAsync() == 0, "Tabela de abastecimentos criada no banco novo");
            }
            var legacyConnection = await CreateDatabase();
            int vehicleId;
            await using (var legacy = Context(legacyConnection))
            {
                var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "schema-v0.1.sql"));
                await legacy.Database.ExecuteSqlRawAsync(Regex.Replace(schema, @"^GO\s*$", "", RegexOptions.Multiline));
                var vehicle = new Vehicle { CompanyId = 1, Plate = "ABC1D23", Brand = "Marca", Model = "Modelo", Year = 2024, Odometer = 1000 };
                legacy.Vehicles.Add(vehicle);
                legacy.Accounts.Add(new Account { Username = "baseline-admin", PasswordHash = "hash-preservado-de-teste" });
                legacy.Drivers.Add(new Driver { CompanyId = 1, Name = "Motorista teste", Cpf = "52998224725", License = "12345678901", LicenseExpiry = new DateOnly(2030, 1, 1) });
                legacy.Audits.Add(new Audit { CompanyId = 1, Actor = "baseline-admin", Action = "Antes da atualização" });
                await legacy.SaveChangesAsync();
                vehicleId = vehicle.Id;
            }
            await using (var upgraded = Context(legacyConnection))
            {
                await upgraded.Database.MigrateAsync();
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
