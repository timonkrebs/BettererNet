using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererResultsFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static JsonArray Arr(params string[] values) => new(values.Select(v => (JsonNode?)v).ToArray());

    [Fact]
    public async Task Load_WhenFileMissing_ReturnsEmpty()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.Empty(file.Results);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsValue_Canonicalized()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", Arr("b", "a"));
        await file.SaveAsync();

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.True(reloaded.TryGet("TestA", out var value));
        Assert.Equal(new[] { "a", "b" }, value!.Deserialize<List<string>>());
    }

    [Fact]
    public void Set_ReturnsFalse_WhenCanonicalValueUnchanged()
    {
        var file = BettererResultsFile.LoadAsync(ResultsPath).Result;

        Assert.True(file.Set("T", Arr("b", "a")));
        // Same set, different order -> canonical value is unchanged.
        Assert.False(file.Set("T", Arr("a", "b")));
        Assert.True(file.Set("T", Arr("a", "b", "c")));
    }

    [Fact]
    public async Task Save_SortsTestNames_AndLeavesNoTempFile()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("Zebra", Arr("z"));
        file.Set("Alpha", Arr("a"));
        await file.SaveAsync();

        var json = await File.ReadAllTextAsync(ResultsPath);

        Assert.True(json.IndexOf("Alpha", StringComparison.Ordinal) < json.IndexOf("Zebra", StringComparison.Ordinal));
        Assert.False(File.Exists(ResultsPath + ".tmp"));
    }

    [Fact]
    public async Task Save_WritesSchemaVersion2()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", Arr("a"));
        await file.SaveAsync();

        var json = await File.ReadAllTextAsync(ResultsPath);

        Assert.Contains("\"version\": 2", json);
    }

    [Fact]
    public async Task Save_HandlesDotNetTypeNames_RoundTripsSortedAndUnescaped()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("My.Namespace.Tests+Nested", Arr("My.Ns.Type`2.Method", "My.Ns.IFoo", "My.Ns.Outer+Inner"));
        await file.SaveAsync();

        var json = await File.ReadAllTextAsync(ResultsPath);
        // The relaxed encoder keeps `+` readable rather than escaping it to +.
        Assert.Contains("My.Ns.Outer+Inner", json);

        var reloaded = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(reloaded.TryGet("My.Namespace.Tests+Nested", out var value));
        Assert.Equal(
            new[] { "My.Ns.IFoo", "My.Ns.Outer+Inner", "My.Ns.Type`2.Method" },
            value!.Deserialize<List<string>>());
    }

    [Fact]
    public async Task Save_WhenEmpty_DeletesFile()
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set("TestA", Arr("x"));
        await file.SaveAsync();
        Assert.True(File.Exists(ResultsPath));

        file.Remove("TestA");
        await file.SaveAsync();

        Assert.False(File.Exists(ResultsPath));
    }

    [Fact]
    public void Remove_ReturnsTrueOnlyWhenPresent()
    {
        var file = BettererResultsFile.LoadAsync(ResultsPath).Result;

        Assert.False(file.Remove("missing"));

        file.Set("TestA", Arr("a"));
        Assert.True(file.Remove("TestA"));
        Assert.False(file.Remove("TestA"));
    }

    [Fact]
    public async Task LoadsLegacyV1Format()
    {
        const string v1 = """
            {
              "version": 1,
              "results": {
                "OldTest": { "timestamp": "2026-01-01T00:00:00+00:00", "issues": ["x", "y"] }
              }
            }
            """;
        await File.WriteAllTextAsync(ResultsPath, v1);

        var file = await BettererResultsFile.LoadAsync(ResultsPath);

        Assert.True(file.TryGet("OldTest", out var value));
        Assert.Equal(new[] { "x", "y" }, value!.Deserialize<List<string>>());
    }
}
