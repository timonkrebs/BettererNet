using Xunit;

namespace BettererNet.Tests;

public sealed class BettererResultsFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Load_WhenFileMissing_ReturnsEmpty()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.Empty(file.Results);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsIssues()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", new BettererStoredResult { Issues = { "b", "a" } });
        await file.SaveAsync();

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.True(reloaded.TryGet("TestA", out var entry));
        Assert.Equal(new[] { "a", "b" }, entry!.Issues);
    }

    [Fact]
    public async Task Save_SortsTestNamesAndIssues_Deterministically()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("Zebra", new BettererStoredResult { Issues = { "z2", "z1" } });
        file.Set("Alpha", new BettererStoredResult { Issues = { "a2", "a1" } });
        await file.SaveAsync();

        var json = await File.ReadAllTextAsync(ResultsPath);

        Assert.True(json.IndexOf("Alpha", StringComparison.Ordinal) < json.IndexOf("Zebra", StringComparison.Ordinal));
        Assert.True(json.IndexOf("a1", StringComparison.Ordinal) < json.IndexOf("a2", StringComparison.Ordinal));
        // Atomic write leaves no temporary file behind.
        Assert.False(File.Exists(ResultsPath + ".tmp"));
    }

    [Fact]
    public async Task Save_WhenEmpty_DeletesFile()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", new BettererStoredResult { Issues = { "x" } });
        await file.SaveAsync();
        Assert.True(File.Exists(ResultsPath));

        file.Remove("TestA");
        await file.SaveAsync();

        Assert.False(File.Exists(ResultsPath));
    }
}
