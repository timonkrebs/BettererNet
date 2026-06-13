namespace BettererNet;

/// <summary>Options that influence how a test run is evaluated.</summary>
public sealed class BettererRunContext
{
    /// <summary>
    /// When <c>true</c>, a regression is accepted and recorded as the new baseline
    /// (reported as <see cref="BettererRunStatus.Updated"/>) instead of failing.
    /// </summary>
    public bool Update { get; init; }

    /// <summary>The instant used to evaluate test deadlines. Defaults to now.</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
}
