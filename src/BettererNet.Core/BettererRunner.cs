namespace BettererNet;

/// <summary>
/// Runs a suite of tests against a results file using Betterer's default semantics: new results
/// are recorded, improvements ratchet the baseline, and regressions fail the suite without
/// overwriting the baseline (unless <see cref="BettererRunContext.Update"/> is set).
/// </summary>
public static class BettererRunner
{
    public static async Task<BettererSuiteSummary> RunAsync(
        IEnumerable<IBettererTest> tests,
        BettererResultsFile resultsFile,
        BettererRunContext? context = null,
        bool write = true,
        CancellationToken cancellationToken = default)
    {
        context ??= new BettererRunContext();

        var runs = new List<BettererRunSummary>();
        var changed = false;

        foreach (var test in tests)
        {
            resultsFile.TryGet(test.Name, out var baselineValue);
            var summary = await test.RunAsync(baselineValue, context, cancellationToken).ConfigureAwait(false);
            runs.Add(summary);

            if (write && summary.ShouldUpdateResults && summary.Result is not null && resultsFile.Set(test.Name, summary.Result))
            {
                changed = true;
            }
        }

        if (write && changed)
        {
            await resultsFile.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        return new BettererSuiteSummary { Runs = runs };
    }
}
