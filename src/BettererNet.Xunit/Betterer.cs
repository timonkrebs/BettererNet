using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Xunit;

namespace BettererNet;

/// <summary>
/// xUnit adapter for Betterer. Compares the issues reported by a test against a stored
/// baseline in the shared <c>.betterer.results</c> file, failing only when new issues
/// appear and ratcheting the baseline down as issues are fixed.
/// </summary>
public sealed class Betterer
{
    // One async gate per results file so parallel xUnit test collections in the same
    // process serialise their read-modify-write of the shared file.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.Ordinal);

    private readonly string _testName;
    private readonly string _resultsPath;

    // Set BETTERER_UPDATE=1 to record missing baselines instead of failing (seeding).
    // Phase 3's CLI `--update` will extend this to accepting regressions too.
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
    /// Assert that <paramref name="testResult"/> introduces no issues that are not already
    /// in the baseline. Improvements are locked in by updating the baseline.
    /// </summary>
    /// <param name="testResult">The issues the test currently reports.</param>
    /// <param name="allowFirstFailure">
    /// When <c>true</c>, the very first run records the reported issues as the baseline
    /// instead of failing. When <c>false</c> (default), a test with issues and no existing
    /// baseline fails so new baselines are an explicit choice.
    /// </param>
    public async Task AssertAsync(BettererResult testResult, bool allowFirstFailure = false)
    {
        var issues = testResult.FailingTypeNames;
        var gate = FileLocks.GetOrAdd(_resultsPath, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();
        try
        {
            var resultsFile = await BettererResultsFile.LoadAsync(_resultsPath);

            // Success: the test reports no issues. Clear any stored baseline.
            if (issues.Count == 0)
            {
                if (resultsFile.Remove(_testName))
                {
                    await resultsFile.SaveAsync();
                }

                return;
            }

            if (!resultsFile.TryGet(_testName, out var baseline))
            {
                // First time this test has run with issues. Fail so that accepting a new
                // baseline is an explicit choice, unless seeding/updating was requested.
                if (!allowFirstFailure && !UpdateRequested)
                {
                    Assert.Empty(issues);
                }

                resultsFile.Set(_testName, new BettererStoredResult { Issues = new List<string>(issues) });
                await resultsFile.SaveAsync();
                return;
            }

            // Subsequent run: fail on any issue not present in the baseline.
            var baselineSet = new HashSet<string>(baseline.Issues, StringComparer.Ordinal);
            var newIssues = issues.Where(issue => !baselineSet.Contains(issue)).ToList();
            Assert.Empty(newIssues);

            // No regression. If the issue set is unchanged the on-disk baseline is already
            // correct, so leave it untouched to keep the results file diff-stable. Only
            // rewrite when issues were fixed, ratcheting the baseline down.
            if (baselineSet.SetEquals(issues))
            {
                return;
            }

            resultsFile.Set(_testName, new BettererStoredResult { Issues = new List<string>(issues) });
            await resultsFile.SaveAsync();
        }
        finally
        {
            gate.Release();
        }
    }

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
