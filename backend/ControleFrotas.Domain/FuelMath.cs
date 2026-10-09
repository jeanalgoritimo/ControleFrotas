namespace ControleFrotas;

public static class FuelMath
{
    public static decimal Total(decimal liters, decimal unitPrice) => decimal.Round(liters * unitPrice, 2, MidpointRounding.AwayFromZero);
    public static decimal? Consumption(bool fullTank, decimal odometer, decimal? previousFullOdometer, decimal litersSinceFull)
    {
        if (!fullTank || previousFullOdometer is null || odometer <= previousFullOdometer || litersSinceFull <= 0) return null;
        return decimal.Round((odometer - previousFullOdometer.Value) / litersSinceFull, 2, MidpointRounding.AwayFromZero);
    }
}
