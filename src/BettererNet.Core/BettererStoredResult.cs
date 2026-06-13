namespace BettererNet;

/// <summary>A single test's persisted baseline within the <see cref="BettererResultsFile"/>.</summary>
public sealed class BettererStoredResult
{
    /// <summary>When this baseline was last written.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The issues recorded as the accepted baseline for the test.</summary>
    public List<string> Issues { get; set; } = new();
}
