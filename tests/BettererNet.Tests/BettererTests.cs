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
    public async Task FewerIssues_RatchetsBaselineDown()
    {
        await NewBetterer().AssertAsync(Result("Issue1", "Issue2"), allowFirstFailure: true);
        await NewBetterer().AssertAsync(Result("Issue1"));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("Test", out var entry));
        Assert.Equal(new[] { "Issue1" }, entry!.Issues);
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
