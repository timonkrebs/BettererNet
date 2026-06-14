using Xunit;

namespace BettererNet.Tests;

public sealed class BettererHistoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string HistoryPath => Path.Combine(_dir, "history.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task AppendSaveLoad_RoundTrips()
    {
        var history = await BettererHistory.LoadAsync(HistoryPath);
        history.Append(new BettererHistorySnapshot
        {
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Counts = new Dictionary<string, long> { ["a"] = 5 },
        });
        history.Append(new BettererHistorySnapshot
        {
            Timestamp = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            Counts = new Dictionary<string, long> { ["a"] = 3 },
        });
        await history.SaveAsync();

        var reloaded = await BettererHistory.LoadAsync(HistoryPath);

        Assert.Equal(2, reloaded.Snapshots.Count);
        Assert.Equal(3, reloaded.Snapshots[1].Counts["a"]);
    }

    [Fact]
    public async Task RenderMarkdown_ShowsTrend()
    {
        var history = await BettererHistory.LoadAsync(HistoryPath);
        history.Append(new BettererHistorySnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Counts = new Dictionary<string, long> { ["TestA"] = 5 },
        });

        var markdown = history.RenderMarkdown();

        Assert.Contains("Betterer trend", markdown);
        Assert.Contains("TestA", markdown);
        Assert.Contains("5", markdown);
    }
}
