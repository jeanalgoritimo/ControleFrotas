namespace ControleFrotas;

public static class FleetEndpoints
{
    public static void MapFleetEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/vehicles", (FleetService s, HttpContext c, CancellationToken ct) => s.Vehicles(IdentityScope.Company(c), ct));
        api.MapPost("/vehicles", async (VehicleRequest r, FleetService s, HttpContext c, CancellationToken ct) => { var v = await s.SaveVehicle(IdentityScope.Company(c), c.User.Identity!.Name!, null, r, ct); return Results.Created($"/api/vehicles/{v.Id}", v); });
        api.MapPut("/vehicles/{id:int}", async (int id, VehicleRequest r, FleetService s, HttpContext c, CancellationToken ct) => Results.Ok(await s.SaveVehicle(IdentityScope.Company(c), c.User.Identity!.Name!, id, r, ct)));
        api.MapGet("/drivers", (FleetService s, HttpContext c, CancellationToken ct) => s.Drivers(IdentityScope.Company(c), ct));
        api.MapPost("/drivers", async (DriverRequest r, FleetService s, HttpContext c, CancellationToken ct) => { var d = await s.SaveDriver(IdentityScope.Company(c), c.User.Identity!.Name!, null, r, ct); return Results.Created($"/api/drivers/{d.Id}", d); });
        api.MapPut("/drivers/{id:int}", async (int id, DriverRequest r, FleetService s, HttpContext c, CancellationToken ct) => Results.Ok(await s.SaveDriver(IdentityScope.Company(c), c.User.Identity!.Name!, id, r, ct)));
        api.MapGet("/audit", (FleetService s, HttpContext c, CancellationToken ct) => s.Audits(IdentityScope.Company(c), ct));
    }
}
