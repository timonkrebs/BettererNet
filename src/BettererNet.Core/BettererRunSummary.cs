using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>The result of running a single Betterer test.</summary>
public sealed class BettererRunSummary
{
    public required string Name { get; init; }

    public required BettererRunStatus Status { get; init; }

    /// <summary>The serialized current result, or <c>null</c> for skipped/failed runs.</summary>
    public JsonNode? Result { get; init; }

    /// <summary>The serialized baseline the result was compared against, if any.</summary>
    public JsonNode? Baseline { get; init; }

    /// <summary>Whether the runner should persist <see cref="Result"/> as the new baseline.</summary>
    public bool ShouldUpdateResults { get; init; }

    /// <summary>The exception thrown by the test function, if <see cref="Status"/> is <see cref="BettererRunStatus.Failed"/>.</summary>
    public Exception? Error { get; init; }

    /// <summary>The owner (person or team) responsible for this test's debt, if it was tagged.</summary>
    public string? Owner { get; init; }

    /// <summary>The configured issue budget (a hard ceiling), if any.</summary>
    public int? Budget { get; init; }

    /// <summary>Whether the result exceeded its <see cref="Budget"/> — which forces the run to fail.</summary>
    public bool IsOverBudget { get; init; }

    /// <summary>Whether this run should fail the suite.</summary>
    public bool IsFailure => Status is BettererRunStatus.Worse or BettererRunStatus.Failed or BettererRunStatus.Expired;

    public static BettererRunSummary Skipped(string name) =>
        new() { Name = name, Status = BettererRunStatus.Skipped, ShouldUpdateResults = false };

    public static BettererRunSummary Failed(string name, Exception error) =>
        new() { Name = name, Status = BettererRunStatus.Failed, Error = error, ShouldUpdateResults = false };
}
