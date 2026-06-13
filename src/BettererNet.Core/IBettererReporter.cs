namespace BettererNet;

/// <summary>Receives results as a suite runs, for rendering output. Implementations must be cheap and non-throwing.</summary>
public interface IBettererReporter
{
    /// <summary>Called once per test with its run result.</summary>
    void ReportRun(BettererRunSummary run);

    /// <summary>Called once after all tests have run.</summary>
    void ReportSuite(BettererSuiteSummary suite);
}
