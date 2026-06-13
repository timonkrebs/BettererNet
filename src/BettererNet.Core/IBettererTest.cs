using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>A runnable Betterer test, independent of its result type.</summary>
public interface IBettererTest
{
    /// <summary>The key the test's result is stored under in the results file.</summary>
    string Name { get; }

    /// <summary>Whether this test is skipped.</summary>
    bool IsSkipped { get; }

    /// <summary>Run the test and compare its result against <paramref name="baselineValue"/>.</summary>
    Task<BettererRunSummary> RunAsync(
        JsonNode? baselineValue,
        BettererRunContext context,
        CancellationToken cancellationToken = default);
}
