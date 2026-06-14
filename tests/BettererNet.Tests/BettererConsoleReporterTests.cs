using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererConsoleReporterTests
{
    [Fact]
    public void PrintsNewIssues_OnRegression()
    {
        var output = new StringWriter();
        var reporter = new BettererConsoleReporter(output);
        var baseline = BettererFileIssuesSerializer.Instance.Serialize(new BettererFileIssues().Add("A.cs", 1, 1, 1, "old"));
        var current = BettererFileIssuesSerializer.Instance.Serialize(
            new BettererFileIssues().Add("A.cs", 1, 1, 1, "old").Add("B.cs", 5, 2, 3, "regression"));

        reporter.ReportRun(new BettererRunSummary { Name = "T", Status = BettererRunStatus.Worse, Result = current, Baseline = baseline });

        var text = output.ToString();
        Assert.Contains("B.cs:5:2", text);
        Assert.Contains("regression", text);
        Assert.DoesNotContain("A.cs", text); // the unchanged issue is not listed
    }

    [Fact]
    public void PassingRun_ListsNoIssues()
    {
        var output = new StringWriter();
        var reporter = new BettererConsoleReporter(output);

        reporter.ReportRun(new BettererRunSummary { Name = "T", Status = BettererRunStatus.Same });

        var text = output.ToString();
        Assert.Contains("[same] T", text);
        Assert.DoesNotContain("+", text);
    }
}
