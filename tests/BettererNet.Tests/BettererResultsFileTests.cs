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
    public async Task SaveThenLoad_PreservesTimestamp()
    {
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", new BettererStoredResult { Timestamp = timestamp, Issues = { "a" } });
        await file.SaveAsync();

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.True(reloaded.TryGet("TestA", out var entry));
        Assert.Equal(timestamp, entry!.Timestamp);
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
    public async Task Save_WritesSchemaVersion()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", new BettererStoredResult { Issues = { "a" } });
        await file.SaveAsync();

        var json = await File.ReadAllTextAsync(ResultsPath);

        Assert.Contains("\"version\": 1", json);
    }

    [Fact]
    public async Task Save_HandlesDotNetTypeNames_RoundTripsAndSorts()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("My.Namespace.Tests+Nested", new BettererStoredResult
        {
            Issues = { "My.Ns.Type`2.Method", "My.Ns.IFoo", "My.Ns.Outer+Inner" },
        });
        await file.SaveAsync();

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.True(reloaded.TryGet("My.Namespace.Tests+Nested", out var entry));
        Assert.Equal(
            new[] { "My.Ns.IFoo", "My.Ns.Outer+Inner", "My.Ns.Type`2.Method" },
            entry!.Issues);
    }

    [Fact]
    public async Task Set_OverwritesExistingEntry()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", new BettererStoredResult { Issues = { "old" } });
        file.Set("TestA", new BettererStoredResult { Issues = { "new" } });

        Assert.True(file.TryGet("TestA", out var entry));
        Assert.Equal(new[] { "new" }, entry!.Issues);
    }

    [Fact]
    public async Task Remove_ReturnsTrueOnlyWhenPresent()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.False(file.Remove("missing"));

        file.Set("TestA", new BettererStoredResult { Issues = { "a" } });
        Assert.True(file.Remove("TestA"));
        Assert.False(file.Remove("TestA"));
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
