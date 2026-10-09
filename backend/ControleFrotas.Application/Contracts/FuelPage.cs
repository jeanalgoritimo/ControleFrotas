namespace ControleFrotas;

public sealed record FuelPage(List<FuelRow> Items, int TotalCount, decimal TotalLiters, decimal TotalCost);
