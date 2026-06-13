using Xunit;
using Xunit.Sdk;

namespace BettererNet.Tests;

public sealed class BettererTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private Betterer NewBetterer(string testName = "Test") => new(testName, ResultsPath);

    private static BettererResult Result(params string[] issues)
    {
        var result = new BettererResult();
        result.FailingTypeNames.AddRange(issues);
        return result;
    }

    [Fact]
    public async Task FirstFailure_WithoutAllow_ThrowsAndWritesNoBaseline()
    {
        await Assert.ThrowsAnyAsync<XunitException>(
            () => NewBetterer().AssertAsync(Result("Issue1")));

        Assert.False(File.Exists(ResultsPath));
    }

    [Fact]
    public async Task FirstFailure_WithAllow_RecordsBaseline()
    {
        await NewBetterer().AssertAsync(Result("Issue1"), allowFirstFailure: true);

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("Test", out var entry));
        Assert.Equal(new[] { "Issue1" }, entry!.Issues);
    }

    [Fact]
    public async Task NoNewIssues_Passes()
    {
        await NewBetterer().AssertAsync(Result("Issue1"), allowFirstFailure: true);

        // Reporting the same issue again is not a regression.
        await NewBetterer().AssertAsync(Result("Issue1"));
    }

    [Fact]
    public async Task NewIssue_Throws()
    {
        await NewBetterer().AssertAsync(Result("Issue1"), allowFirstFailure: true);

        await Assert.ThrowsAnyAsync<XunitException>(
            () => NewBetterer().AssertAsync(Result("Issue1", "Issue2")));
    }

    [Fact]
    public async Task Regression_DoesNotModifyBaseline()
    {
        await NewBetterer().AssertAsync(Result("Issue1"), allowFirstFailure: true);

        await Assert.ThrowsAnyAsync<XunitException>(
            () => NewBetterer().AssertAsync(Result("Issue1", "Issue2")));

        // The accepted baseline must not be overwritten by the worse result.
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("Test", out var entry));
        Assert.Equal(new[] { "Issue1" }, entry!.Issues);
    }

    [Fact]
    public async Task FewerIssues_RatchetsBaselineDown()
    {
        await NewBetterer().AssertAsync(Result("Issue1", "Issue2"), allowFirstFailure: true);
        await NewBetterer().AssertAsync(Result("Issue1"));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("Test", out var entry));
        Assert.Equal(new[] { "Issue1" }, entry!.Issues);
    }

    [Fact]
    public async Task ReorderedIssues_AreNotARegression_AndDoNotRewriteFile()
    {
        await NewBetterer().AssertAsync(Result("A", "B", "C"), allowFirstFailure: true);
        var before = await File.ReadAllTextAsync(ResultsPath);

        // Same set, different order: passes and leaves the results file byte-for-byte unchanged.
        await NewBetterer().AssertAsync(Result("C", "A", "B"));
        var after = await File.ReadAllTextAsync(ResultsPath);

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task DifferentTestNames_AreIsolated()
    {
        await NewBetterer("TestA").AssertAsync(Result("A1"), allowFirstFailure: true);
        await NewBetterer("TestB").AssertAsync(Result("B1"), allowFirstFailure: true);

        // A regression in TestB must not disturb TestA's accepted baseline.
        await Assert.ThrowsAnyAsync<XunitException>(
            () => NewBetterer("TestB").AssertAsync(Result("B1", "B2")));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("TestA", out var a));
        Assert.Equal(new[] { "A1" }, a!.Issues);
        Assert.True(file.TryGet("TestB", out var b));
        Assert.Equal(new[] { "B1" }, b!.Issues);
    }

    [Fact]
    public async Task Success_RemovesBaseline()
    {
        await NewBetterer().AssertAsync(Result("Issue1"), allowFirstFailure: true);
        Assert.True(File.Exists(ResultsPath));

        await NewBetterer().AssertAsync(Result());

        Assert.False(File.Exists(ResultsPath));
    }
}
