namespace BettererNet;

/// <summary>The aggregated result of running a suite of Betterer tests.</summary>
public sealed class BettererSuiteSummary
{
    public required IReadOnlyList<BettererRunSummary> Runs { get; init; }

    /// <summary>Whether any test in the suite failed (worse, failed, or expired).</summary>
    public bool IsFailure => Runs.Any(run => run.IsFailure);

    /// <summary>The number of runs that ended in the given status.</summary>
    public int CountOf(BettererRunStatus status) => Runs.Count(run => run.Status == status);
}
