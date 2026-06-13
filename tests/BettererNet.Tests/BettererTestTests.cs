using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererTestTests
{
    private static JsonNode Count(long value) => JsonValue.Create(value)!;

    private static BettererRunContext Context(bool update = false, DateTimeOffset? now = null) =>
        new() { Update = update, Now = now ?? DateTimeOffset.UtcNow };

    [Fact]
    public async Task New_WhenNoBaseline()
    {
        var summary = await BettererCountTest.Create("t", () => 5).RunAsync(null, Context());

        Assert.Equal(BettererRunStatus.New, summary.Status);
        Assert.True(summary.ShouldUpdateResults);
        Assert.False(summary.IsFailure);
        Assert.Equal(5, summary.Result!.GetValue<long>());
    }

    [Fact]
    public async Task Better_WhenSmaller()
    {
        var summary = await BettererCountTest.Create("t", () => 5).RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Better, summary.Status);
        Assert.True(summary.ShouldUpdateResults);
    }

    [Fact]
    public async Task Same_WhenEqual()
    {
        var summary = await BettererCountTest.Create("t", () => 10).RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Same, summary.Status);
    }

    [Fact]
    public async Task Worse_WhenBigger_FailsAndDoesNotUpdate()
    {
        var summary = await BettererCountTest.Create("t", () => 15).RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Worse, summary.Status);
        Assert.False(summary.ShouldUpdateResults);
        Assert.True(summary.IsFailure);
    }

    [Fact]
    public async Task Updated_WhenWorseButUpdateRequested()
    {
        var summary = await BettererCountTest.Create("t", () => 15).RunAsync(Count(10), Context(update: true));

        Assert.Equal(BettererRunStatus.Updated, summary.Status);
        Assert.True(summary.ShouldUpdateResults);
        Assert.False(summary.IsFailure);
    }

    [Fact]
    public async Task Complete_WhenGoalMet()
    {
        var summary = await BettererCountTest.Create("t", () => 0, goal: 0).RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Complete, summary.Status);
        Assert.False(summary.IsFailure);
    }

    [Fact]
    public async Task Expired_WhenDeadlinePassedAndGoalUnmet()
    {
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var summary = await BettererCountTest.Create("t", () => 5, goal: 0, deadline: now.AddDays(-1))
            .RunAsync(Count(10), Context(now: now));

        Assert.Equal(BettererRunStatus.Expired, summary.Status);
        Assert.True(summary.IsFailure);
    }

    [Fact]
    public async Task GoalBeatsExpiry_WhenGoalMetAfterDeadline()
    {
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var summary = await BettererCountTest.Create("t", () => 0, goal: 0, deadline: now.AddDays(-1))
            .RunAsync(Count(10), Context(now: now));

        Assert.Equal(BettererRunStatus.Complete, summary.Status);
        Assert.False(summary.IsFailure);
    }

    [Fact]
    public async Task Failed_WhenTestThrows()
    {
        var test = new BettererTest<long>(
            "t",
            () => throw new InvalidOperationException("boom"),
            BettererConstraints.Smaller);

        var summary = await test.RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Failed, summary.Status);
        Assert.True(summary.IsFailure);
        Assert.IsType<InvalidOperationException>(summary.Error);
    }

    [Fact]
    public async Task Skipped_WhenIsSkipped()
    {
        var test = new BettererTest<long>("t", () => 5, BettererConstraints.Smaller, isSkipped: true);

        var summary = await test.RunAsync(Count(10), Context());

        Assert.Equal(BettererRunStatus.Skipped, summary.Status);
        Assert.False(summary.ShouldUpdateResults);
    }
}
