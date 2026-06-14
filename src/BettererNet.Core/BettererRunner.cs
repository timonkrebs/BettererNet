namespace BettererNet;

/// <summary>
/// Runs a suite of tests against a results file using Betterer's default semantics: new results
/// are recorded, improvements ratchet the baseline, and regressions fail the suite without
/// overwriting the baseline (unless <see cref="BettererRunContext.Update"/> is set). When a
/// <see cref="BettererCache"/> is supplied, tests whose input fingerprint is unchanged are skipped.
/// </summary>
public static class BettererRunner
{
    public static async Task<BettererSuiteSummary> RunAsync(
        IEnumerable<IBettererTest> tests,
        BettererResultsFile resultsFile,
        BettererRunContext? context = null,
        bool write = true,
        int maxDegreeOfParallelism = 1,
        BettererCache? cache = null,
        CancellationToken cancellationToken = default)
    {
        context ??= new BettererRunContext();

        var testList = tests.ToList();
        var summaries = new BettererRunSummary[testList.Count];
        var fingerprints = new string?[testList.Count];
        var toRun = new List<int>();

        // Decide cache hits first (computes input fingerprints). A test with an unchanged fingerprint
        // and an existing baseline is skipped: its result is, by definition, the baseline.
        for (var i = 0; i < testList.Count; i++)
        {
            var test = testList[i];
            if (cache is not null)
            {
                fingerprints[i] = test.ComputeFingerprint();
                if (fingerprints[i] is { } fingerprint
                    && cache.TryGet(test.Name, out var cached) && cached == fingerprint
                    && resultsFile.TryGet(test.Name, out var baseline))
                {
                    summaries[i] = new BettererRunSummary
                    {
                        Name = test.Name,
                        Status = BettererRunStatus.Same,
                        Result = baseline,
                        Baseline = baseline,
                        ShouldUpdateResults = false,
                    };
                    continue;
                }
            }

            toRun.Add(i);
        }

        async Task RunAtAsync(int index)
        {
            var test = testList[index];
            resultsFile.TryGet(test.Name, out var baseline);
            summaries[index] = await test.RunAsync(baseline, context, cancellationToken).ConfigureAwait(false);
        }

        if (maxDegreeOfParallelism <= 1 || toRun.Count <= 1)
        {
            foreach (var index in toRun)
            {
                await RunAtAsync(index).ConfigureAwait(false);
            }
        }
        else
        {
            using var throttle = new SemaphoreSlim(maxDegreeOfParallelism);
            await Task.WhenAll(toRun.Select(async index =>
            {
                await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await RunAtAsync(index).ConfigureAwait(false);
                }
                finally
                {
                    throttle.Release();
                }
            })).ConfigureAwait(false);
        }

        // Apply results and cache writes single-threaded so both files stay consistent.
        var resultsChanged = false;
        var cacheChanged = false;
        for (var i = 0; i < testList.Count; i++)
        {
            var summary = summaries[i];
            if (write && summary.ShouldUpdateResults && summary.Result is not null && resultsFile.Set(summary.Name, summary.Result))
            {
                resultsChanged = true;
            }

            // Cache the fingerprint only when the result is recorded (not a failure), so the cache
            // tracks the inputs of the accepted baseline.
            if (cache is not null && fingerprints[i] is { } fingerprint && !summary.IsFailure && cache.Set(summary.Name, fingerprint))
            {
                cacheChanged = true;
            }
        }

        if (write && resultsChanged)
        {
            await resultsFile.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        if (cache is not null && cacheChanged)
        {
            await cache.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        return new BettererSuiteSummary { Runs = summaries.ToList() };
    }
}
