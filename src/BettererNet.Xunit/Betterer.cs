using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet;

/// <summary>
/// xUnit adapter for Betterer. Runs a test on the shared engine, compares its result against the
/// baseline in the shared <c>.betterer.results</c> file, and fails the xUnit test when the result
/// is new (without opt-in), regresses, or expires — ratcheting the baseline as results improve.
/// </summary>
public sealed class Betterer
{
    // One async gate per results file so parallel xUnit test collections in the same
    // process serialise their read-modify-write of the shared file.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.Ordinal);

    private readonly string _testName;
    private readonly string _resultsPath;

    // Set BETTERER_UPDATE=1 to accept new results and regressions and record them as the new
    // baseline, mirroring betterer's `--update`.
    private static bool UpdateRequested =>
        Environment.GetEnvironmentVariable("BETTERER_UPDATE") is "1" or "true" or "TRUE";

    /// <param name="testName">
    /// The key the result is stored under. Defaults to the calling test method's name.
    /// </param>
    /// <param name="resultsPath">
    /// Path to the results file. Defaults to <c>.betterer.results</c> in the test project directory.
    /// </param>
    public Betterer([CallerMemberName] string testName = "", string? resultsPath = null)
    {
        _testName = testName;
        _resultsPath = resultsPath ?? Path.Combine(FindProjectDirectory(), BettererResultsFile.DefaultFileName);
    }

    /// <summary>
    /// Assert that the reported issues introduce nothing not already in the baseline. Improvements
    /// are locked in by ratcheting the baseline down.
    /// </summary>
    /// <param name="testResult">The issues the test currently reports.</param>
    /// <param name="allowFirstFailure">
    /// When <c>true</c>, the first run records the reported issues as the baseline instead of
    /// failing. When <c>false</c> (default), a test with issues and no baseline fails so accepting
    /// a new baseline is an explicit choice.
    /// </param>
    public Task AssertAsync(BettererResult testResult, bool allowFirstFailure = false)
    {
        var issues = testResult.FailingTypeNames;
        var test = new BettererTest<List<string>>(
            _testName,
            () => new List<string>(issues),
            BettererConstraints.SetBased<string>(),
            JsonBettererSerializer<List<string>>.Instance);

        return AssertAsync(test, allowFirstFailure);
    }

    /// <summary>
    /// Run any Betterer test (counting, file, custom) and assert it did not get worse. The test's
    /// own <see cref="IBettererTest.Name"/> is used unless this instance was given an explicit name.
    /// </summary>
    public async Task AssertAsync(IBettererTest test, bool allowFirstFailure = false)
    {
        var update = UpdateRequested;
        var gate = FileLocks.GetOrAdd(_resultsPath, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();
        try
        {
            var resultsFile = await BettererResultsFile.LoadAsync(_resultsPath);
            resultsFile.TryGet(test.Name, out var baselineValue);
            var summary = await test.RunAsync(baselineValue, new BettererRunContext { Update = update });

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
                    await resultsFile.SaveAsync();
                }

                return;
            }

            switch (summary.Status)
            {
                case BettererRunStatus.New when !allowFirstFailure && !update:
                    Assert.Fail(
                        $"Betterer test '{test.Name}' has results and no baseline. Run with " +
                        "allowFirstFailure: true or set BETTERER_UPDATE=1 to accept them.");
                    return;

                case BettererRunStatus.Worse:
                case BettererRunStatus.Expired:
                    Assert.Fail(
                        $"Betterer test '{test.Name}' got worse than its recorded baseline. Fix the " +
                        "regression, or set BETTERER_UPDATE=1 to accept it.");
                    return;

                default:
                    if (summary.ShouldUpdateResults && resultsFile.Set(test.Name, summary.Result))
                    {
                        await resultsFile.SaveAsync();
                    }

                    return;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        _ => false,
    };

    // Walk up from the test assembly's output directory (…/bin/<config>/<tfm>) to the
    // project directory so the results file lives next to the project, not in bin.
    private static string FindProjectDirectory()
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
}
