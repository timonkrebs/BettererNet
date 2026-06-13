namespace BettererNet.Cli;

/// <summary>Options shared by the CLI commands.</summary>
public sealed class BettererCliOptions
{
    /// <summary>Path to the results file.</summary>
    public string ResultsPath { get; init; } = BettererResultsFile.DefaultFileName;

    /// <summary>Test-name regex filters. A leading <c>!</c> negates. Empty means "all tests".</summary>
    public IReadOnlyList<string> Filters { get; init; } = [];

    /// <summary>Accept regressions and record them as the new baseline (betterer's <c>--update</c>).</summary>
    public bool Update { get; init; }

    /// <summary>Hide guidance about updating results.</summary>
    public bool Strict { get; init; }

    /// <summary>Suppress the default reporter.</summary>
    public bool Silent { get; init; }

    /// <summary>An explicit reporter. When null, one is chosen from <see cref="Silent"/>.</summary>
    public IBettererReporter? Reporter { get; init; }
}
