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
        int maxDegreeOfParallelism = 1,
        CancellationToken cancellationToken = default)
    {
        context ??= new BettererRunContext();

        // Read baselines up front (cheap, single-threaded), then run the (possibly expensive) tests.
        var inputs = tests.Select(test =>
        {
            resultsFile.TryGet(test.Name, out var baseline);
            return (Test: test, Baseline: baseline);
        }).ToList();

        BettererRunSummary[] summaries;
        if (maxDegreeOfParallelism <= 1 || inputs.Count <= 1)
        {
            summaries = new BettererRunSummary[inputs.Count];
            for (var i = 0; i < inputs.Count; i++)
            {
                summaries[i] = await inputs[i].Test.RunAsync(inputs[i].Baseline, context, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            using var throttle = new SemaphoreSlim(maxDegreeOfParallelism);
            summaries = await Task.WhenAll(inputs.Select(async input =>
            {
                await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await input.Test.RunAsync(input.Baseline, context, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    throttle.Release();
                }
            })).ConfigureAwait(false);
        }

        // Apply writes single-threaded so the results file stays consistent.
        var changed = false;
        foreach (var summary in summaries)
        {
            if (write && summary.ShouldUpdateResults && summary.Result is not null && resultsFile.Set(summary.Name, summary.Result))
            {
                changed = true;
            }
        }

        if (write && changed)
        {
            await resultsFile.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        return new BettererSuiteSummary { Runs = summaries.ToList() };
    }
}
