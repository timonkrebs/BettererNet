using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Framework-agnostic orchestration shared by the test-framework adapters. Runs a test against the
/// shared results file and invokes <c>fail</c> (which is expected to throw the test framework's
/// assertion) when the result is new without opt-in, regresses, or expires. Improvements ratchet
/// the baseline; a clean result clears it.
/// </summary>
public static class BettererAssertions
{
    // One async gate per results file so parallel test collections serialise their read-modify-write.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.Ordinal);

    // BETTERER_UPDATE=1 records new baselines and accepts regressions (betterer's --update).
    private static bool UpdateRequested =>
        Environment.GetEnvironmentVariable("BETTERER_UPDATE") is "1" or "true" or "TRUE";

    /// <summary>Assert over a reported set of issue names (the <see cref="BettererResult"/> shape).</summary>
    public static Task AssertAsync(string testName, string resultsPath, BettererResult result, bool allowFirstFailure, Action<string> fail)
    {
        var issues = result.FailingTypeNames;
        var test = new BettererTest<List<string>>(
            testName,
            () => new List<string>(issues),
            BettererConstraints.SetBased<string>(),
            JsonBettererSerializer<List<string>>.Instance);
        return AssertAsync(resultsPath, test, allowFirstFailure, fail);
    }

    /// <summary>Assert over any engine test, keyed by <see cref="IBettererTest.Name"/>.</summary>
    public static async Task AssertAsync(string resultsPath, IBettererTest test, bool allowFirstFailure, Action<string> fail)
    {
        var update = UpdateRequested;
        var gate = FileLocks.GetOrAdd(resultsPath, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var resultsFile = await BettererResultsFile.LoadAsync(resultsPath).ConfigureAwait(false);
            resultsFile.TryGet(test.Name, out var baselineValue);
            var summary = await test.RunAsync(baselineValue, new BettererRunContext { Update = update }).ConfigureAwait(false);

            if (summary.Status == BettererRunStatus.Skipped)
            {
                return;
            }

            if (summary.Status == BettererRunStatus.Failed)
            {
                ExceptionDispatchInfo.Throw(summary.Error!);
            }

            // A clean result (no issues) clears any stored baseline and always passes.
            if (IsEmpty(summary.Result))
            {
                if (resultsFile.Remove(test.Name))
                {
                    await resultsFile.SaveAsync().ConfigureAwait(false);
                }

                return;
            }

            switch (summary.Status)
            {
                case BettererRunStatus.New when !allowFirstFailure && !update:
                    fail($"Betterer test '{test.Name}' has results and no baseline. Run with " +
                        "allowFirstFailure: true or set BETTERER_UPDATE=1 to accept them.");
                    return;

                case BettererRunStatus.Worse:
                case BettererRunStatus.Expired:
                    fail($"Betterer test '{test.Name}' got worse than its recorded baseline. Fix the " +
                        "regression, or set BETTERER_UPDATE=1 to accept it.");
                    return;

                default:
                    if (summary.ShouldUpdateResults && resultsFile.Set(test.Name, summary.Result))
                    {
                        await resultsFile.SaveAsync().ConfigureAwait(false);
                    }

                    return;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The project directory (parent of <c>bin</c>) used for the default results-file path.</summary>
    public static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (string.Equals(directory.Name, "bin", StringComparison.OrdinalIgnoreCase) && directory.Parent is not null)
            {
                return directory.Parent.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        _ => false,
    };
}
