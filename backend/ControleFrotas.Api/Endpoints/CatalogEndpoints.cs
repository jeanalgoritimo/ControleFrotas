namespace ControleFrotas;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/vehicle-brands", (bool? includeInactive, CatalogService s, HttpContext c, CancellationToken ct) => s.Brands(IdentityScope.Company(c), includeInactive ?? false, ct));
        api.MapGet("/vehicle-models", (int brandId, bool? includeInactive, CatalogService s, HttpContext c, CancellationToken ct) => s.Models(IdentityScope.Company(c), brandId, includeInactive ?? false, ct));
        api.MapPost("/vehicle-brands", async (BrandRequest r, CatalogService s, HttpContext c, CancellationToken ct) => { var item = await s.SaveBrand(IdentityScope.Company(c), c.User.Identity!.Name!, null, r, ct); return Results.Created($"/api/vehicle-brands/{item.Id}", item); });
        api.MapPut("/vehicle-brands/{id:int}", async (int id, BrandRequest r, CatalogService s, HttpContext c, CancellationToken ct) => Results.Ok(await s.SaveBrand(IdentityScope.Company(c), c.User.Identity!.Name!, id, r, ct)));
        api.MapPost("/vehicle-models", async (ModelRequest r, CatalogService s, HttpContext c, CancellationToken ct) => { var item = await s.SaveModel(IdentityScope.Company(c), c.User.Identity!.Name!, null, r, ct); return Results.Created($"/api/vehicle-models/{item.Id}", item); });
        api.MapPut("/vehicle-models/{id:int}", async (int id, ModelRequest r, CatalogService s, HttpContext c, CancellationToken ct) => Results.Ok(await s.SaveModel(IdentityScope.Company(c), c.User.Identity!.Name!, id, r, ct)));
    }
}
