namespace ControleFrotas;

public sealed record FuelWindow(DateOnly? LastDate, decimal? FullOdometer, decimal LitersSinceFull);
