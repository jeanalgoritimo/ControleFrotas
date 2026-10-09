using ControleFrotas;
using Microsoft.EntityFrameworkCore;

public static class CatalogIntegration
{
    public static async Task<int> Run(string connection)
    {
        var checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
        FleetDb Context() => new(new DbContextOptionsBuilder<FleetDb>().UseSqlServer(connection).Options);
        async Task Rejected<T>(Func<FleetDb, Task> action) where T : Exception
        {
            await using var db = Context();
            try { await action(db); throw new Exception($"Esperava {typeof(T).Name}"); }
            catch (T) { checks++; }
        }
        int brandId, modelId, secondBrandId, vehicleId;
        byte[] staleBrandVersion;
        await using (var db = Context())
        {
            var service = new CatalogService(new CatalogStore(db));
            var brand = await service.SaveBrand(1, "admin", null, new BrandRequest(" Toyota ", true, null), default);
            var model = await service.SaveModel(1, "admin", null, new ModelRequest(brand.Id, " Corolla ", true, null), default);
            brandId = brand.Id; modelId = model.Id; staleBrandVersion = brand.Version;
            secondBrandId = (await service.SaveBrand(1, "admin", null, new BrandRequest("Honda", true, null), default)).Id;
            var vehicle = await new FleetService(new FleetStore(db), new CatalogStore(db)).SaveVehicle(1, "admin", null,
                new VehicleRequest("ABC1D23", "Carro", "Ignorada", "Ignorado", 2024, 0, true, null, brandId, modelId), default);
            vehicleId = vehicle.Id;
            Check(vehicle.Brand == "Toyota" && vehicle.Model == "Corolla", "Veículo recebe nomes canônicos persistidos");
            Check((await service.Models(1, brandId, false, default)).Single().Id == modelId && (await service.Models(1, secondBrandId, false, default)).Count == 0, "Consulta retorna somente modelos da marca");
            Check((await service.Brands(2, true, default)).Count == 0 && (await service.Models(2, brandId, true, default)).Count == 0, "Catálogo SQL isolado por empresa");
        }
        await Rejected<DbUpdateException>(db => new CatalogService(new CatalogStore(db)).SaveBrand(1, "admin", null, new BrandRequest(" toyota ", true, null), default));
        await Rejected<DbUpdateException>(db => new CatalogService(new CatalogStore(db)).SaveModel(1, "admin", null, new ModelRequest(brandId, " corolla ", true, null), default));
        await using (var db = Context())
        {
            Check(await db.VehicleBrands.CountAsync() == 2 && await db.VehicleModels.CountAsync() == 1 && await db.Audits.CountAsync() == 4, "Duplicidades não gravam catálogo nem auditoria");
            var brand = await db.VehicleBrands.SingleAsync(b => b.Id == brandId);
            await new CatalogService(new CatalogStore(db)).SaveBrand(1, "admin", brandId, new BrandRequest("Toyota Brasil", true, brand.Version), default);
        }
        await Rejected<DbUpdateConcurrencyException>(db => new CatalogService(new CatalogStore(db)).SaveBrand(1, "admin", brandId, new BrandRequest("Nome antigo", true, staleBrandVersion), default));
        await using (var db = Context())
        {
            Check((await db.Vehicles.SingleAsync(v => v.Id == vehicleId)).Brand == "Toyota Brasil" && await db.Audits.CountAsync() == 5, "Renomear atualiza veículos e conflito não grava auditoria");
            var service = new CatalogService(new CatalogStore(db));
            await service.SaveModel(1, "admin", null, new ModelRequest(secondBrandId, "Corolla", true, null), default);
            Check(await db.VehicleModels.CountAsync() == 2, "Mesmo nome de modelo permitido em marcas diferentes");
            var brand = await db.VehicleBrands.SingleAsync(b => b.Id == brandId);
            await service.SaveBrand(1, "admin", brandId, new BrandRequest(brand.Name, false, brand.Version), default);
            Check((await service.Models(1, brandId, false, default)).Count == 0 && (await service.Models(1, brandId, true, default)).Count == 1, "Marca inativa oculta modelos somente nas novas seleções");
        }
        await Rejected<DbUpdateException>(async db =>
        {
            db.Vehicles.Add(new Vehicle { CompanyId = 1, Plate = "DEF1D23", Brand = "Honda", Model = "Corolla", BrandId = secondBrandId, ModelId = modelId, Year = 2024 });
            await db.SaveChangesAsync();
        });
        await Rejected<DbUpdateException>(async db =>
        {
            db.Vehicles.Add(new Vehicle { CompanyId = 2, Plate = "DEF1D23", Brand = "Toyota", Model = "Corolla", BrandId = brandId, ModelId = modelId, Year = 2024 });
            await db.SaveChangesAsync();
        });
        return checks;
    }
}
