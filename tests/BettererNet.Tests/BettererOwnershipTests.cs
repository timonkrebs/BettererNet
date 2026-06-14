using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererOwnershipTests
{
    private static JsonNode Count(long value) => JsonValue.Create(value)!;

    private static BettererRunContext Context() => new() { Now = DateTimeOffset.UtcNow };

    [Fact]
    public void WithOwnership_NoMetadata_ReturnsSameInstance()
    {
        var inner = BettererCountTest.Create("t", () => 1);

        Assert.Same(inner, inner.WithOwnership());
    }

    [Fact]
    public async Task Owner_FlowsToSummary()
    {
        var test = BettererCountTest.Create("t", () => 1).WithOwnership(owner: "team-platform");

        var summary = await test.RunAsync(Count(1), Context());

        Assert.Equal("team-platform", summary.Owner);
        Assert.False(summary.IsOverBudget);
    }

    [Fact]
    public async Task Budget_FailsAnImprovementThatIsStillOverBudget()
    {
        // 15 is an improvement over the baseline of 20, but it still exceeds the budget of 10.
        var test = BettererCountTest.Create("t", () => 15).WithOwnership(budget: 10);

        var summary = await test.RunAsync(Count(20), Context());

        Assert.Equal(BettererRunStatus.Worse, summary.Status);
        Assert.True(summary.IsFailure);
        Assert.True(summary.IsOverBudget);
        Assert.False(summary.ShouldUpdateResults);
        Assert.Equal(10, summary.Budget);
    }

    [Fact]
    public async Task Budget_PassesWhenUnderBudget()
    {
        var test = BettererCountTest.Create("t", () => 5).WithOwnership(owner: "team-a", budget: 10);

        var summary = await test.RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Better, summary.Status);
        Assert.False(summary.IsOverBudget);
        Assert.True(summary.ShouldUpdateResults);
    }

    [Fact]
    public async Task Budget_NewResultOverBudget_IsNotSeeded()
    {
        var test = BettererCountTest.Create("t", () => 15).WithOwnership(budget: 10);

        var summary = await test.RunAsync(null, Context());

        Assert.Equal(BettererRunStatus.Worse, summary.Status);
        Assert.True(summary.IsOverBudget);
        Assert.False(summary.ShouldUpdateResults); // an over-budget baseline must never be recorded
    }

    [Fact]
    public void Budget_IsFoldedIntoTheFingerprint()
    {
        var inner = new BettererTest<long>("t", () => 1, BettererConstraints.Smaller, fingerprint: () => "abc");

        Assert.NotEqual(inner.WithOwnership(budget: 10).ComputeFingerprint(), inner.WithOwnership(budget: 20).ComputeFingerprint());
    }

    [Fact]
    public void Fingerprint_StaysNullWhenInnerIsUncacheable()
    {
        var inner = BettererCountTest.Create("t", () => 1); // no fingerprint

        Assert.Null(inner.WithOwnership(budget: 10).ComputeFingerprint());
    }
}
