using System.Text.Json;
using NetArchTest.Rules;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererArchTestTests
{
    [Fact]
    public async Task CapturesFailingTypeNames()
    {
        var test = BettererArchTest.Create("interfaces", () =>
            Types.InAssembly(typeof(BettererArchTestTests).Assembly)
                .That().AreInterfaces()
                .Should().HaveNameStartingWith("I")
                .GetResult());

        var summary = await test.RunAsync(null, new BettererRunContext());
        var names = summary.Result!.Deserialize<List<string>>();

        // The deliberately misnamed interface below violates the rule.
        Assert.Contains(names!, name => name.EndsWith("BadlyNamedContract", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PassingRule_IsClean()
    {
        var test = BettererArchTest.Create("classes", () =>
            Types.InAssembly(typeof(BettererArchTestTests).Assembly)
                .That().HaveNameStartingWith("ZzNonExistentPrefix")
                .Should().BePublic()
                .GetResult());

        var summary = await test.RunAsync(null, new BettererRunContext());
        var names = summary.Result!.Deserialize<List<string>>();

        Assert.Empty(names!);
    }
}

// Intentionally violates "interfaces start with I", to be detected by the arch test above.
public interface BadlyNamedContract
{
}
