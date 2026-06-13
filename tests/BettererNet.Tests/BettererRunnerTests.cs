using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererRunnerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task RecordsNewResults_AndReportsSuccess()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        var tests = new IBettererTest[]
        {
            BettererCountTest.Create("a", () => 3),
            BettererCountTest.Create("b", () => 0, goal: 0),
        };

        var summary = await BettererRunner.RunAsync(tests, file);

        Assert.False(summary.IsFailure);
        Assert.Equal(1, summary.CountOf(BettererRunStatus.New));
        Assert.Equal(1, summary.CountOf(BettererRunStatus.Complete));

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(reloaded.TryGet("a", out var a));
        Assert.Equal(3, a!.GetValue<long>());
        Assert.True(reloaded.TryGet("b", out var b));
        Assert.Equal(0, b!.GetValue<long>());
    }

    [Fact]
    public async Task WorseResult_FailsSuite_AndDoesNotOverwriteBaseline()
    {
        var seed = await BettererResultsFile.LoadAsync(ResultsPath);
        seed.Set("a", JsonValue.Create(5L));
        await seed.SaveAsync();

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        var summary = await BettererRunner.RunAsync(new IBettererTest[] { BettererCountTest.Create("a", () => 8) }, file);

        Assert.True(summary.IsFailure);
        Assert.Equal(1, summary.CountOf(BettererRunStatus.Worse));

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(5, reloaded.Results["a"].GetValue<long>());
    }

    [Fact]
    public async Task IsByteStable_AcrossRuns()
    {
        IBettererTest[] Tests() => new IBettererTest[]
        {
            BettererCountTest.Create("a", () => 3),
            BettererCountTest.Create("b", () => 7),
        };

        await BettererRunner.RunAsync(Tests(), await BettererResultsFile.LoadAsync(ResultsPath));
        var first = await File.ReadAllTextAsync(ResultsPath);

        await BettererRunner.RunAsync(Tests(), await BettererResultsFile.LoadAsync(ResultsPath));
        var second = await File.ReadAllTextAsync(ResultsPath);

        Assert.Equal(first, second);
    }
}
