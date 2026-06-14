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

    /// <summary>Maximum number of tests to run concurrently. 1 (default) runs sequentially.</summary>
    public int Workers { get; init; } = 1;

    /// <summary>
    /// Name of a built-in reporter (<c>console</c>, <c>github</c>, <c>silent</c>). Used only when
    /// <see cref="Reporter"/> is null and <see cref="Silent"/> is false.
    /// </summary>
    public string? ReporterName { get; init; }

    /// <summary>When set, also write a SARIF 2.1.0 report of the current issues to this path.</summary>
    public string? SarifPath { get; init; }

    /// <summary>
    /// An explicit reporter, taking precedence over the rest. When null, the reporter is chosen by
    /// <see cref="Silent"/> first, then <see cref="ReporterName"/>, defaulting to the console reporter.
    /// </summary>
    public IBettererReporter? Reporter { get; init; }
}
