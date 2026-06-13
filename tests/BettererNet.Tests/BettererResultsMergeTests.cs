using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererResultsMergeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static JsonArray Arr(params string[] values) => new(values.Select(v => (JsonNode?)v).ToArray());

    [Fact]
    public void Numbers_TakeMinimum()
    {
        var ours = BettererResultsFile.Create("ours");
        ours.Set("count", JsonValue.Create(3L));
        var theirs = BettererResultsFile.Create("theirs");
        theirs.Set("count", JsonValue.Create(5L));

        var merged = BettererResultsMerge.Merge(ours, theirs);

        Assert.Equal(3, merged["count"].GetValue<long>());
    }

    [Fact]
    public void Arrays_TakeIntersection()
    {
        var ours = BettererResultsFile.Create("ours");
        ours.Set("a", Arr("x", "y"));
        var theirs = BettererResultsFile.Create("theirs");
        theirs.Set("a", Arr("y", "z"));

        var merged = BettererResultsMerge.Merge(ours, theirs);

        Assert.Equal(new[] { "y" }, merged["a"].Deserialize<List<string>>());
    }

    [Fact]
    public void Objects_IntersectFilesAndIssues()
    {
        var ours = BettererResultsFile.Create("ours");
        ours.Set("t", new JsonObject { ["A.cs"] = Arr("i1", "i2"), ["B.cs"] = Arr("i3") });
        var theirs = BettererResultsFile.Create("theirs");
        theirs.Set("t", new JsonObject { ["A.cs"] = Arr("i2", "i4") });

        var merged = BettererResultsMerge.Merge(ours, theirs).ToDictionary(p => p.Key, p => p.Value);
        var value = merged["t"].AsObject();

        Assert.False(value.ContainsKey("B.cs")); // fixed on theirs -> dropped
        Assert.Equal(new[] { "i2" }, value["A.cs"]!.Deserialize<List<string>>());
    }

    [Fact]
    public void TestPresentInOnlyOneSide_IsKept()
    {
        var ours = BettererResultsFile.Create("ours");
        ours.Set("only", JsonValue.Create(2L));
        var theirs = BettererResultsFile.Create("theirs");

        var merged = BettererResultsMerge.Merge(ours, theirs);

        Assert.True(merged.ContainsKey("only"));
    }

    [Fact]
    public async Task MergeFilesAsync_WritesTightestBaseline()
    {
        var oursPath = Path.Combine(_dir, "ours.results");
        var theirsPath = Path.Combine(_dir, "theirs.results");

        var ours = BettererResultsFile.Create(oursPath);
        ours.Set("count", JsonValue.Create(3L));
        await ours.SaveAsync();
        var theirs = BettererResultsFile.Create(theirsPath);
        theirs.Set("count", JsonValue.Create(5L));
        await theirs.SaveAsync();

        await BettererResultsMerge.MergeFilesAsync(oursPath, theirsPath, oursPath);

        var merged = await BettererResultsFile.LoadAsync(oursPath);
        Assert.Equal(3, merged.Results["count"].GetValue<long>());
    }
}
