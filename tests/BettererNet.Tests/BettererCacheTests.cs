using Xunit;

namespace BettererNet.Tests;

public sealed class BettererCacheTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    private string CachePath => Path.Combine(_dir, BettererCache.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Fingerprint_IsStable_AndChangesWithContent()
    {
        var file = Path.Combine(_dir, "a.txt");
        File.WriteAllText(file, "one");

        var first = BettererFileFingerprint.Compute(new[] { file });
        Assert.Equal(first, BettererFileFingerprint.Compute(new[] { file }));

        File.WriteAllText(file, "two");
        Assert.NotEqual(first, BettererFileFingerprint.Compute(new[] { file }));
    }

    [Fact]
    public async Task Cache_RoundTrips()
    {
        var cache = await BettererCache.LoadAsync(CachePath);
        Assert.True(cache.Set("a", "fp1"));
        Assert.False(cache.Set("a", "fp1"));
        await cache.SaveAsync();

        var reloaded = await BettererCache.LoadAsync(CachePath);
        Assert.True(reloaded.TryGet("a", out var fingerprint));
        Assert.Equal("fp1", fingerprint);
    }

    [Fact]
    public async Task Runner_SkipsWhenFingerprintUnchanged_AndRerunsWhenChanged()
    {
        var runs = 0;
        BettererTest<long> Test(string fingerprint) =>
            new("a", () => { runs++; return 1L; }, BettererConstraints.Smaller, fingerprint: () => fingerprint);

        // First run: nothing cached -> runs, records baseline and fingerprint.
        await BettererRunner.RunAsync([Test("fp1")], await BettererResultsFile.LoadAsync(ResultsPath), cache: await BettererCache.LoadAsync(CachePath));
        Assert.Equal(1, runs);

        // Same fingerprint -> skipped.
        await BettererRunner.RunAsync([Test("fp1")], await BettererResultsFile.LoadAsync(ResultsPath), cache: await BettererCache.LoadAsync(CachePath));
        Assert.Equal(1, runs);

        // Changed fingerprint -> re-runs.
        await BettererRunner.RunAsync([Test("fp2")], await BettererResultsFile.LoadAsync(ResultsPath), cache: await BettererCache.LoadAsync(CachePath));
        Assert.Equal(2, runs);
    }
}
