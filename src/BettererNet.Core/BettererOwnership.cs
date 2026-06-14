using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>Attaches ownership and an optional issue budget to any test, for large-team triage.</summary>
public static class BettererTestExtensions
{
    /// <summary>
    /// Tag a test with an <paramref name="owner"/> (a person or team, surfaced in reports so debt can
    /// be routed) and/or a <paramref name="budget"/> — a hard ceiling on the issue count. A run over
    /// budget fails the suite even when it improved on, or matched, its baseline, and is never
    /// recorded (so a baseline can't be seeded above the ceiling). Returns the test unchanged when
    /// neither is supplied.
    /// </summary>
    public static IBettererTest WithOwnership(this IBettererTest test, string? owner = null, int? budget = null) =>
        owner is null && budget is null ? test : new OwnedBettererTest(test, owner, budget);
}

/// <summary>Decorates a test, carrying ownership metadata and enforcing a budget on its result.</summary>
internal sealed class OwnedBettererTest : IBettererTest
{
    private readonly IBettererTest _inner;
    private readonly string? _owner;
    private readonly int? _budget;

    public OwnedBettererTest(IBettererTest inner, string? owner, int? budget)
    {
        _inner = inner;
        _owner = owner;
        _budget = budget;
    }

    public string Name => _inner.Name;

    public bool IsSkipped => _inner.IsSkipped;

    // Fold the budget into the fingerprint: lowering it must invalidate a cached (now over-budget) result.
    public string? ComputeFingerprint() =>
        _inner.ComputeFingerprint() is { } fingerprint ? $"{fingerprint}|budget={_budget?.ToString() ?? "none"}" : null;

    public async Task<BettererRunSummary> RunAsync(
        JsonNode? baselineValue,
        BettererRunContext context,
        CancellationToken cancellationToken = default)
    {
        var summary = await _inner.RunAsync(baselineValue, context, cancellationToken).ConfigureAwait(false);

        var status = summary.Status;
        var shouldUpdate = summary.ShouldUpdateResults;
        var overBudget = _budget is { } budget
            && status is not (BettererRunStatus.Skipped or BettererRunStatus.Failed)
            && BettererCount.Of(summary.Result) > budget;

        if (overBudget)
        {
            // A budget is a hard ceiling — fail the run and never persist an over-budget baseline.
            status = BettererRunStatus.Worse;
            shouldUpdate = false;
        }

        return new BettererRunSummary
        {
            Name = summary.Name,
            Status = status,
            Result = summary.Result,
            Baseline = summary.Baseline,
            ShouldUpdateResults = shouldUpdate,
            Error = summary.Error,
            Owner = _owner,
            Budget = _budget,
            IsOverBudget = overBudget,
        };
    }
}
