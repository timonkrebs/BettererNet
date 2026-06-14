using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererHistoryReporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task RecordsSnapshot_AndRendersMarkdown()
    {
        var historyPath = Path.Combine(_dir, ".betterer.history.json");
        var reporter = new BettererHistoryReporter(historyPath);
        var result = BettererFileIssuesSerializer.Instance.Serialize(
            new BettererFileIssues().Add("A.cs", 1, 1, 1, "x").Add("A.cs", 2, 1, 1, "y"));
        var run = new BettererRunSummary { Name = "Analyzer", Status = BettererRunStatus.Same, Result = result };

        reporter.ReportRun(run);
        reporter.ReportSuite(new BettererSuiteSummary { Runs = new[] { run } });

        var history = await BettererHistory.LoadAsync(historyPath);
        Assert.Single(history.Snapshots);
        Assert.Equal(2, history.Snapshots[0].Counts["Analyzer"]);
        Assert.True(File.Exists(Path.ChangeExtension(historyPath, ".md")));
    }
}
