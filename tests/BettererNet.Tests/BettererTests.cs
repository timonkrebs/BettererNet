using System.Text.Json;
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

    private async Task<List<string>?> ReadBaseline(string testName = "Test")
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        return file.TryGet(testName, out var value) ? value!.Deserialize<List<string>>() : null;
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

        Assert.Equal(new[] { "Issue1" }, await ReadBaseline());
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
        Assert.Equal(new[] { "Issue1" }, await ReadBaseline());
    }

    [Fact]
    public async Task FewerIssues_RatchetsBaselineDown()
    {
        await NewBetterer().AssertAsync(Result("Issue1", "Issue2"), allowFirstFailure: true);
        await NewBetterer().AssertAsync(Result("Issue1"));

        Assert.Equal(new[] { "Issue1" }, await ReadBaseline());
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

        Assert.Equal(new[] { "A1" }, await ReadBaseline("TestA"));
        Assert.Equal(new[] { "B1" }, await ReadBaseline("TestB"));
    }

    [Fact]
    public async Task Success_RemovesBaseline()
    {
        await NewBetterer().AssertAsync(Result("Issue1"), allowFirstFailure: true);
        Assert.True(File.Exists(ResultsPath));

        await NewBetterer().AssertAsync(Result());

        Assert.False(File.Exists(ResultsPath));
    }

    [Fact]
    public async Task GenericOverload_CountTest_RegressionThrows()
    {
        await new Betterer("Count", ResultsPath).AssertAsync(BettererCountTest.Create("Count", () => 2), allowFirstFailure: true);

        await Assert.ThrowsAnyAsync<XunitException>(
            () => new Betterer("Count", ResultsPath).AssertAsync(BettererCountTest.Create("Count", () => 5)));
    }

    [Fact]
    public async Task GenericOverload_CountTest_ImprovementIsRecorded()
    {
        await new Betterer("Count", ResultsPath).AssertAsync(BettererCountTest.Create("Count", () => 5), allowFirstFailure: true);
        await new Betterer("Count", ResultsPath).AssertAsync(BettererCountTest.Create("Count", () => 3));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(3, file.Results["Count"].GetValue<long>());
    }
}
