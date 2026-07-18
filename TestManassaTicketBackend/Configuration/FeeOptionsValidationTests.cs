using manassa_ticket_backend.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TestManassaTicketBackend.Configuration;

// Mirrors the AddOptions<FeeOptions>() registration in Program.cs (which isn't itself
// unit-testable, being top-level statements) to verify the negative-value guard actually
// throws rather than trusting the .Validate() pattern blindly.
public class FeeOptionsValidationTests
{
    private static IOptions<FeeOptions> BuildOptions(string flatFeeUsd, string percentFee)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fee:FlatFeeUsd"] = flatFeeUsd,
                ["Fee:PercentFee"] = percentFee
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<FeeOptions>()
            .Bind(configuration.GetSection("Fee"))
            .Validate(o => o.FlatFeeUsd >= 0, "Fee:FlatFeeUsd must not be negative.")
            .Validate(o => o.PercentFee >= 0, "Fee:PercentFee must not be negative.");

        return services.BuildServiceProvider().GetRequiredService<IOptions<FeeOptions>>();
    }

    [Test]
    public void Value_Throws_WhenFlatFeeUsdIsNegative()
    {
        var options = BuildOptions(flatFeeUsd: "-1", percentFee: "5");

        var act = () => options.Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Test]
    public void Value_Throws_WhenPercentFeeIsNegative()
    {
        var options = BuildOptions(flatFeeUsd: "1", percentFee: "-5");

        var act = () => options.Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Test]
    public void Value_Succeeds_WhenBothAreNonNegative()
    {
        var options = BuildOptions(flatFeeUsd: "1", percentFee: "5");

        var act = () => options.Value;

        act.Should().NotThrow();
        options.Value.FlatFeeUsd.Should().Be(1m);
        options.Value.PercentFee.Should().Be(5m);
    }

    [Test]
    public void Value_Succeeds_WhenBothAreZero()
    {
        var options = BuildOptions(flatFeeUsd: "0", percentFee: "0");

        var act = () => options.Value;

        act.Should().NotThrow();
    }
}
