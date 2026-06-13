namespace BettererNet.Cli;

/// <summary>Writes a concise, plain-text summary of each run and the suite to the console.</summary>
public sealed class BettererConsoleReporter : IBettererReporter
{
    public void ReportRun(BettererRunSummary run)
    {
        Console.WriteLine($"  [{run.Status.ToString().ToLowerInvariant()}] {run.Name}");
        if (run.Status == BettererRunStatus.Failed && run.Error is not null)
        {
            Console.WriteLine($"      {run.Error.Message}");
        }
    }

    public void ReportSuite(BettererSuiteSummary suite)
    {
        var verdict = suite.IsFailure ? "FAILED" : "OK";
        Console.WriteLine(
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
