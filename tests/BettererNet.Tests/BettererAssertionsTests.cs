using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererAssertionsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static IBettererTest Count(string name, long value) => BettererCountTest.Create(name, () => value);

    private static void Fail(string message) => throw new FailSentinel(message);

    private async Task Seed(string name, long value)
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set(name, JsonValue.Create(value));
        await file.SaveAsync();
    }

    [Fact]
    public async Task New_WithoutAllow_Fails_AndWritesNothing()
    {
        await Assert.ThrowsAsync<FailSentinel>(() =>
            BettererAssertions.AssertAsync(ResultsPath, Count("a", 3), allowFirstFailure: false, Fail));

        Assert.False(File.Exists(ResultsPath));
    }

    [Fact]
    public async Task New_WithAllow_RecordsBaseline()
    {
        await BettererAssertions.AssertAsync(ResultsPath, Count("a", 3), allowFirstFailure: true, Fail);

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(3, file.Results["a"].GetValue<long>());
    }

    [Fact]
    public async Task Worse_Fails_AndKeepsBaseline()
    {
        await Seed("a", 2);

        await Assert.ThrowsAsync<FailSentinel>(() =>
            BettererAssertions.AssertAsync(ResultsPath, Count("a", 5), allowFirstFailure: false, Fail));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(2, file.Results["a"].GetValue<long>());
    }

    [Fact]
    public async Task Better_RatchetsDown_WithoutFailing()
    {
        await Seed("a", 5);

        await BettererAssertions.AssertAsync(ResultsPath, Count("a", 3), allowFirstFailure: false, Fail);

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(3, file.Results["a"].GetValue<long>());
    }

    private sealed class FailSentinel(string message) : Exception(message);
}
