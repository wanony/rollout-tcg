using TCGTrading.Alerts.Domain.Enums;

namespace TCGTrading.Alerts.Domain;

public static class CrossingPolicy
{
    // Fires on a genuine edge crossing into the trigger zone.
    // hysteresisFraction requires the previous price to have been clear of the
    // boundary by target*fraction, so oscillation on the line doesn't re-fire.
    public static bool ShouldFire(
        AlertDirection direction, decimal target, decimal? lastPrice,
        decimal newPrice, decimal hysteresisFraction)
    {
        var band = target * hysteresisFraction;
        return direction switch
        {
            AlertDirection.Below =>
                newPrice <= target &&
                (lastPrice is null ? true : lastPrice > target + band),
            AlertDirection.Above =>
                newPrice >= target &&
                (lastPrice is null ? true : lastPrice < target - band),
            _ => false,
        };
    }
}
