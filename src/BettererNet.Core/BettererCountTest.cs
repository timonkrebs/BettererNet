namespace BettererNet;

/// <summary>Factory for counting tests, where fewer is better (the common case for issue counts).</summary>
public static class BettererCountTest
{
    /// <summary>
    /// Create a counting test. The test passes while the count stays the same or shrinks, and
    /// fails when it grows. When <paramref name="goal"/> is supplied, reaching it (count at or
    /// below the goal) marks the test complete.
    /// </summary>
    public static BettererTest<long> Create(
        string name,
        Func<long> count,
        long? goal = null,
        DateTimeOffset? deadline = null) =>
        new(
            name,
            count,
            BettererConstraints.Smaller,
            goal: goal is { } threshold ? value => value <= threshold : null,
            deadline: deadline);
}
