using Xunit;

namespace BettererNet.Tests;

public sealed class BettererConcurrencyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ConcurrentAsserts_OnSharedFile_DoNotLoseUpdates()
    {
        const int count = 25;

        // Many tests writing to the same results file at once. The per-file lock must
        // serialise the read-modify-write so no entry is lost to a racing write.
        var tasks = Enumerable.Range(0, count).Select(i =>
        {
            var result = new BettererResult();
            result.FailingTypeNames.Add($"Issue{i:D2}");
            return new Betterer($"Test{i:D2}", ResultsPath).AssertAsync(result, allowFirstFailure: true);
        });

        await Task.WhenAll(tasks);

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(count, file.Results.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.True(file.TryGet($"Test{i:D2}", out var entry));
            Assert.Equal(new[] { $"Issue{i:D2}" }, entry!.Issues);
        }
    }
}
