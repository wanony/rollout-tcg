using FluentAssertions;
using Microsoft.Extensions.Hosting;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

namespace TCGTrading.SharedKernel.Tests.Infrastructure.Telemetry;

public class TelemetryExtensionsTests
{
    [Fact]
    public void AddTelemetry_BuildsHostWithoutThrowing()
    {
        var builder = Host.CreateApplicationBuilder();

        var act = () =>
        {
            builder.AddTelemetry("test-service");
            using var host = builder.Build();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AddTelemetry_ReturnsBuilder_AllowingFluentChaining()
    {
        var builder = Host.CreateApplicationBuilder();

        var result = builder.AddTelemetry("test-service");

        result.Should().BeSameAs(builder);
    }
}
