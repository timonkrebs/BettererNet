namespace BettererNet;

/// <summary>The outcome of comparing a test's current result against its baseline.</summary>
public enum BettererConstraintResult
{
    /// <summary>The result improved relative to the baseline.</summary>
    Better,

    /// <summary>The result is equivalent to the baseline.</summary>
    Same,

    /// <summary>The result regressed relative to the baseline.</summary>
    Worse,
}
