namespace ControleFrotas;

public sealed class CatalogService(ICatalogStore store)
{
    public Task<List<VehicleBrand>> Brands(int company, bool includeInactive, CancellationToken ct) => store.Brands(company, includeInactive, ct);
    public Task<List<VehicleModel>> Models(int company, int brandId, bool includeInactive, CancellationToken ct)
    {
        if (brandId <= 0) throw new BusinessException("Selecione uma marca para consultar os modelos.");
        return store.Models(company, brandId, includeInactive, ct);
    }
    public async Task<VehicleBrand> SaveBrand(int company, string actor, int? id, BrandRequest request, CancellationToken ct)
    {
        var name = Name(request.Name, 80, "da marca");
        var brand = id.HasValue ? await store.Brand(company, id.Value, ct) ?? throw new RecordNotFoundException() : new VehicleBrand { CompanyId = company };
        RequireVersion(id, request.Version);
        brand.Name = name; brand.NormalizedName = name.ToUpperInvariant(); brand.Active = request.Active;
        await store.SaveBrand(brand, request.Version, Log(company, actor, id.HasValue ? "Marca editada" : "Marca criada"), !id.HasValue, ct);
        return brand;
    }
    public async Task<VehicleModel> SaveModel(int company, string actor, int? id, ModelRequest request, CancellationToken ct)
    {
        var name = Name(request.Name, 100, "do modelo");
        var brand = await store.Brand(company, request.BrandId, ct) ?? throw new BusinessException("Selecione uma marca cadastrada nesta empresa.");
        var model = id.HasValue ? await store.Model(company, id.Value, ct) ?? throw new RecordNotFoundException() : new VehicleModel { CompanyId = company, BrandId = brand.Id };
        RequireVersion(id, request.Version);
        if (model.BrandId != brand.Id) throw new BusinessException("A marca de um modelo cadastrado não pode ser alterada.");
        if (!brand.Active && (!id.HasValue || request.Active)) throw new BusinessException("Ative a marca antes de cadastrar ou ativar um modelo.");
        model.Name = name; model.NormalizedName = name.ToUpperInvariant(); model.Active = request.Active;
        await store.SaveModel(model, request.Version, Log(company, actor, id.HasValue ? "Modelo editado" : "Modelo criado"), !id.HasValue, ct);
        return model;
    }
    private static string Name(string? value, int maximum, string field)
    {
        var name = value?.Trim() ?? "";
        if (name.Length == 0 || name.Length > maximum || name.Any(char.IsControl)) throw new BusinessException($"Informe o nome {field}, com até {maximum} caracteres.");
        return name;
    }
    private static void RequireVersion(int? id, byte[]? version)
    {
        if (id.HasValue && (version is null || version.Length != 8)) throw new BusinessException("Versão do registro obrigatória. Atualize os dados e tente novamente.");
    }
    private static Audit Log(int company, string actor, string action) => new() { CompanyId = company, Actor = actor, Action = action };
}
