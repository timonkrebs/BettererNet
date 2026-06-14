namespace BettererNet.Cli;

/// <summary>Plain-text reporter: a line per run (with the new issues on a regression) and a suite summary.</summary>
public sealed class BettererConsoleReporter : IBettererReporter
{
    private const int MaxIssuesShown = 10;

    private readonly TextWriter _output;

    public BettererConsoleReporter(TextWriter? output = null) => _output = output ?? Console.Out;

    public void ReportRun(BettererRunSummary run)
    {
        var owner = run.Owner is { } o ? $" (owner: {o})" : "";
        _output.WriteLine($"  [{run.Status.ToString().ToLowerInvariant()}] {run.Name}{owner}");

        if (run.Status == BettererRunStatus.Failed && run.Error is not null)
        {
            _output.WriteLine($"      {run.Error.Message}");
            return;
        }

        if (run.IsOverBudget)
        {
            _output.WriteLine($"      over budget: {BettererCount.Of(run.Result)} exceeds budget of {run.Budget}");
        }

        if (run.Status is not (BettererRunStatus.Worse or BettererRunStatus.Expired))
        {
            return;
        }

        var newIssues = BettererRunDiff.NewIssues(run);
        foreach (var (file, issue) in newIssues.Take(MaxIssuesShown))
        {
            _output.WriteLine($"      + {file}:{issue.Line}:{issue.Column} {issue.Message}");
        }

        if (newIssues.Count > MaxIssuesShown)
        {
            _output.WriteLine($"      ... and {newIssues.Count - MaxIssuesShown} more");
        }
    }

    public void ReportSuite(BettererSuiteSummary suite)
    {
        var verdict = suite.IsFailure ? "FAILED" : "OK";
        _output.WriteLine(
            $"{verdict}: {suite.Runs.Count} test(s) — " +
            $"{suite.CountOf(BettererRunStatus.Better)} better, " +
            $"{suite.CountOf(BettererRunStatus.Worse)} worse, " +
            $"{suite.CountOf(BettererRunStatus.New)} new, " +
            $"{suite.CountOf(BettererRunStatus.Complete)} complete.");
    }
}

/// <summary>A reporter that produces no output.</summary>
public sealed class BettererSilentReporter : IBettererReporter
{
    public void ReportRun(BettererRunSummary run)
    {
    }

    public void ReportSuite(BettererSuiteSummary suite)
    {
    }
}
