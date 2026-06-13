using Xunit;

namespace BettererNet.Tests;

public sealed class BettererFileTestTests
{
    [Fact]
    public void Add_AccumulatesIssuesAndCount()
    {
        var issues = new BettererFileIssues()
            .Add("A.cs", 1, 1, 3, "msg1")
            .Add("A.cs", 5, 2, 4, "msg2")
            .Add("B.cs", 1, 1, 1, "msg3");

        Assert.Equal(3, issues.TotalCount);
        Assert.Equal(2, issues.Files["A.cs"].Count);
    }

    [Fact]
    public void IssueHash_IsLineIndependent_ButContentSensitive()
    {
        var onLine1 = new BettererFileIssues().Add("A.cs", 1, 5, 3, "same").Files["A.cs"][0].Hash;
        var onLine99 = new BettererFileIssues().Add("A.cs", 99, 5, 3, "same").Files["A.cs"][0].Hash;
        var different = new BettererFileIssues().Add("A.cs", 1, 5, 3, "different").Files["A.cs"][0].Hash;

        Assert.Equal(onLine1, onLine99);
        Assert.NotEqual(onLine1, different);
    }

    [Fact]
    public void Serializer_RoundTrips()
    {
        var issues = new BettererFileIssues().Add("A.cs", 10, 5, 3, "bad").Add("A.cs", 1, 1, 1, "early");

        var node = BettererFileIssuesSerializer.Instance.Serialize(issues);
        var roundTripped = BettererFileIssuesSerializer.Instance.Deserialize(node);

        Assert.Equal(2, roundTripped.TotalCount);
        Assert.Equal(
            issues.Files["A.cs"].Select(issue => issue.Hash).OrderBy(hash => hash),
            roundTripped.Files["A.cs"].Select(issue => issue.Hash).OrderBy(hash => hash));
    }

    [Fact]
    public void Diff_ReportsNewAndFixed()
    {
        var baseline = new BettererFileIssues().Add("A.cs", 1, 1, 1, "keep").Add("A.cs", 2, 1, 1, "gone");
        var current = new BettererFileIssues().Add("A.cs", 1, 1, 1, "keep").Add("A.cs", 9, 1, 1, "added");

        var diff = BettererFileIssues.Diff(baseline, current);

        Assert.Equal("added", Assert.Single(diff.New).Message);
        Assert.Equal("gone", Assert.Single(diff.Fixed).Message);
    }

    [Fact]
    public void Diff_IgnoresLineMoves()
    {
        var baseline = new BettererFileIssues().Add("A.cs", 1, 1, 1, "x");
        var current = new BettererFileIssues().Add("A.cs", 50, 1, 1, "x");

        Assert.True(BettererFileIssues.Diff(baseline, current).IsUnchanged);
    }

    [Fact]
    public async Task FileTest_FewerIssues_IsBetter()
    {
        var baseline = BettererFileIssuesSerializer.Instance.Serialize(
            new BettererFileIssues().Add("A.cs", 1, 1, 1, "a").Add("A.cs", 2, 1, 1, "b"));
        var test = BettererFileTest.Create("t", () => new BettererFileIssues().Add("A.cs", 1, 1, 1, "a"));

        var summary = await test.RunAsync(baseline, new BettererRunContext());

        Assert.Equal(BettererRunStatus.Better, summary.Status);
    }

    [Fact]
    public async Task FileTest_MoreIssues_IsWorse()
    {
        var baseline = BettererFileIssuesSerializer.Instance.Serialize(new BettererFileIssues().Add("A.cs", 1, 1, 1, "a"));
        var test = BettererFileTest.Create("t", () => new BettererFileIssues().Add("A.cs", 1, 1, 1, "a").Add("A.cs", 2, 1, 1, "b"));

        var summary = await test.RunAsync(baseline, new BettererRunContext());

        Assert.Equal(BettererRunStatus.Worse, summary.Status);
        Assert.True(summary.IsFailure);
    }

    [Fact]
    public async Task FileTest_SameCount_IsSame_AndDoesNotRequestUpdate()
    {
        var baseline = BettererFileIssuesSerializer.Instance.Serialize(new BettererFileIssues().Add("A.cs", 1, 1, 1, "a"));
        var test = BettererFileTest.Create("t", () => new BettererFileIssues().Add("A.cs", 1, 1, 1, "a"));

        var summary = await test.RunAsync(baseline, new BettererRunContext());

        Assert.Equal(BettererRunStatus.Same, summary.Status);
        Assert.False(summary.ShouldUpdateResults);
    }

    [Fact]
    public async Task FileTest_ZeroIssues_WithGoal_IsComplete()
    {
        var baseline = BettererFileIssuesSerializer.Instance.Serialize(new BettererFileIssues().Add("A.cs", 1, 1, 1, "a"));
        var test = BettererFileTest.Create("t", () => new BettererFileIssues(), goal: BettererFileTest.NoIssues);

        var summary = await test.RunAsync(baseline, new BettererRunContext());

        Assert.Equal(BettererRunStatus.Complete, summary.Status);
        Assert.False(summary.IsFailure);
    }
}
