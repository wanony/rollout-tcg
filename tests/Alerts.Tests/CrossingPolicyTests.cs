using TCGTrading.Alerts.Domain;
using TCGTrading.Alerts.Domain.Enums;
using Xunit;

public class CrossingPolicyTests
{
    const decimal Hyst = 0.0m; // disable hysteresis for the basic cases

    [Fact] public void Below_fires_on_downward_crossing()
        => Assert.True(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 12m, newPrice: 9m, Hyst));

    [Fact] public void Below_does_not_fire_when_already_below_and_staying_below()
        => Assert.False(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 9m, newPrice: 8m, Hyst));

    [Fact] public void Below_fires_on_first_observation_when_already_below()
        => Assert.True(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: null, newPrice: 8m, Hyst));

    [Fact] public void Below_does_not_fire_on_first_observation_when_above()
        => Assert.False(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: null, newPrice: 12m, Hyst));

    [Fact] public void Above_fires_on_upward_crossing()
        => Assert.True(CrossingPolicy.ShouldFire(AlertDirection.Above, 10m, lastPrice: 8m, newPrice: 11m, Hyst));

    [Fact] public void Hysteresis_suppresses_flapping_near_boundary()
    {
        // target 10, 5% band → downward crossing only counts if last price was > 10.5
        Assert.False(CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 10.2m, newPrice: 9.9m, 0.05m));
        Assert.True (CrossingPolicy.ShouldFire(AlertDirection.Below, 10m, lastPrice: 11m,   newPrice: 9.9m, 0.05m));
    }
}
