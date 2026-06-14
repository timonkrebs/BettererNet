namespace BettererNet;

/// <summary>
/// Factory for file tests: tests that report a set of issues per file. The result gets better as
/// the total issue count shrinks and worse as it grows (issues may be swapped freely at the same
/// count). This is the shape the Phase 2 Roslyn/analyzer/coverage integrations build on.
/// </summary>
public static class BettererFileTest
{
    private static readonly BettererConstraint<BettererFileIssues> CountConstraint =
        (current, baseline) => BettererConstraints.Smaller(current.TotalCount, baseline.TotalCount);

    /// <summary>A goal of zero issues — the test completes once the file(s) are clean.</summary>
    public static Func<BettererFileIssues, bool> NoIssues => issues => issues.TotalCount == 0;

    public static BettererTest<BettererFileIssues> Create(
        string name,
        Func<CancellationToken, Task<BettererFileIssues>> test,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null,
        Func<string?>? fingerprint = null) =>
        new(name, test, CountConstraint, BettererFileIssuesSerializer.Instance, goal, deadline, fingerprint: fingerprint);

    public static BettererTest<BettererFileIssues> Create(
        string name,
        Func<BettererFileIssues> test,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null,
        Func<string?>? fingerprint = null) =>
        Create(name, _ => Task.FromResult(test()), goal, deadline, fingerprint);
}
