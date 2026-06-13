namespace BettererNet;

/// <summary>The outcome of running a single Betterer test against its baseline.</summary>
public enum BettererRunStatus
{
    /// <summary>First run of this test — no baseline existed yet.</summary>
    New,

    /// <summary>The result improved; the baseline ratchets to the new result.</summary>
    Better,

    /// <summary>The result is unchanged.</summary>
    Same,

    /// <summary>The result regressed. The baseline is not updated and the run fails.</summary>
    Worse,

    /// <summary>The test met its goal.</summary>
    Complete,

    /// <summary>The result regressed but updating was requested, so the baseline was overwritten.</summary>
    Updated,

    /// <summary>The test was skipped and did not run.</summary>
    Skipped,

    /// <summary>The test function threw an exception.</summary>
    Failed,

    /// <summary>The test's deadline passed before its goal was met. The run fails.</summary>
    Expired,
}
