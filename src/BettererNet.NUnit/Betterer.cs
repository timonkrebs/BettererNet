using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace BettererNet;

/// <summary>
/// NUnit adapter for Betterer. A thin wrapper over <see cref="BettererAssertions"/> that fails the
/// NUnit test (via <c>Assert.Fail</c>) when a result is new without opt-in, regresses, or expires.
/// </summary>
public sealed class Betterer
{
    private readonly string _testName;
    private readonly string _resultsPath;

    /// <param name="testName">
    /// The key the result is stored under. Defaults to the calling test method's name.
    /// </param>
    /// <param name="resultsPath">
    /// Path to the results file. Defaults to <c>.betterer.results</c> in the test project directory.
    /// </param>
    public Betterer([CallerMemberName] string testName = "", string? resultsPath = null)
    {
        _testName = testName;
        _resultsPath = resultsPath ?? Path.Combine(BettererAssertions.FindProjectDirectory(), BettererResultsFile.DefaultFileName);
    }

    /// <summary>Assert that the reported issues introduce nothing not already in the baseline.</summary>
    public Task AssertAsync(BettererResult testResult, bool allowFirstFailure = false) =>
        BettererAssertions.AssertAsync(_testName, _resultsPath, testResult, allowFirstFailure, message => Assert.Fail(message));

    /// <summary>Run any Betterer test (counting, file, custom) and assert it did not get worse.</summary>
    public Task AssertAsync(IBettererTest test, bool allowFirstFailure = false) =>
        BettererAssertions.AssertAsync(_resultsPath, test, allowFirstFailure, message => Assert.Fail(message));
}
