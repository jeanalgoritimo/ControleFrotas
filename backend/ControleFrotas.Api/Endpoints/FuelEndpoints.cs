namespace ControleFrotas;

public static class FuelEndpoints
{
    public static void MapFuelEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/fuel").RequireAuthorization();
        api.MapGet("", (int? vehicleId, DateOnly? from, DateOnly? to, int? page, FuelService service, HttpContext c, CancellationToken ct) => service.List(IdentityScope.Company(c), new FuelQuery(vehicleId ?? 0, from, to, page ?? 1), ct));
        api.MapPost("", async (FuelRequest request, FuelService service, HttpContext c, CancellationToken ct) =>
        {
            var entry = await service.Register(IdentityScope.Company(c), c.User.Identity!.Name!, request, ct);
            return Results.Ok(new { entry.Id, entry.Total, entry.KmPerLiter });
        });
    }
}
