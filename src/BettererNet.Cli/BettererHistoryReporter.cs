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

        // IBettererReporter is synchronous. Run the async I/O on a thread-pool thread so we never
        // block a captured synchronization context (e.g. an ASP.NET request thread).
        Task.Run(async () =>
        {
            var history = await BettererHistory.LoadAsync(_historyPath).ConfigureAwait(false);
            history.Append(new BettererHistorySnapshot { Timestamp = DateTimeOffset.UtcNow, Counts = counts });
            await history.SaveAsync().ConfigureAwait(false);
            File.WriteAllText(Path.ChangeExtension(_historyPath, ".md"), history.RenderMarkdown());
        }).GetAwaiter().GetResult();
    }
}
