namespace BettererNet.Cli;

/// <summary>
/// Appends a per-test count snapshot to a history file each run and renders a markdown burn-down
/// alongside it (at the same path with a <c>.md</c> extension). Wired via <c>--history</c>.
/// </summary>
public sealed class BettererHistoryReporter : IBettererReporter
{
    private readonly string _historyPath;

    public BettererHistoryReporter(string historyPath) => _historyPath = historyPath;

    public void ReportRun(BettererRunSummary run)
    {
    }

    public void ReportSuite(BettererSuiteSummary suite)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var run in suite.Runs)
        {
            counts[run.Name] = BettererCount.Of(run.Result);
        }

        // Reporters are synchronous; this runs in a console process with no captured sync context.
        var history = BettererHistory.LoadAsync(_historyPath).GetAwaiter().GetResult();
        history.Append(new BettererHistorySnapshot { Timestamp = DateTimeOffset.UtcNow, Counts = counts });
        history.SaveAsync().GetAwaiter().GetResult();

        File.WriteAllText(Path.ChangeExtension(_historyPath, ".md"), history.RenderMarkdown());
    }
}
