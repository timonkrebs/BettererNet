using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererMarkdownReporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void WritesSummary_WithVerdictTableAndNewIssues()
    {
        var path = Path.Combine(_dir, "betterer.md");
        var reporter = new BettererMarkdownReporter(path);
        var baseline = BettererFileIssuesSerializer.Instance.Serialize(new BettererFileIssues().Add("A.cs", 1, 1, 1, "old"));
        var current = BettererFileIssuesSerializer.Instance.Serialize(
            new BettererFileIssues().Add("A.cs", 1, 1, 1, "old").Add("B.cs", 7, 2, 3, "regression"));
        var worse = new BettererRunSummary { Name = "Analyzer", Status = BettererRunStatus.Worse, Result = current, Baseline = baseline };
        var same = new BettererRunSummary { Name = "Coverage", Status = BettererRunStatus.Same };

        reporter.ReportRun(worse);
        reporter.ReportRun(same);
        reporter.ReportSuite(new BettererSuiteSummary { Runs = new[] { worse, same } });

        var markdown = File.ReadAllText(path);
        Assert.Contains("FAILED", markdown);
        Assert.Contains("| Analyzer | worse |", markdown);
        Assert.Contains("| Coverage | same |", markdown);
        Assert.Contains("### New issues", markdown);
        Assert.Contains("B.cs:7", markdown);
        Assert.Contains("regression", markdown);
    }
}
