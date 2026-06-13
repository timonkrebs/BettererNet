namespace BettererNet;

/// <summary>Compares a test's current result against its baseline.</summary>
public delegate BettererConstraintResult BettererConstraint<in T>(T current, T baseline);

/// <summary>Built-in constraints for common test shapes.</summary>
public static class BettererConstraints
{
    /// <summary>For counting tests where a smaller number is an improvement (e.g. fewer issues).</summary>
    public static readonly BettererConstraint<long> Smaller =
        (current, baseline) => current < baseline ? BettererConstraintResult.Better
            : current > baseline ? BettererConstraintResult.Worse
            : BettererConstraintResult.Same;

    /// <summary>For counting tests where a bigger number is an improvement (e.g. more coverage).</summary>
    public static readonly BettererConstraint<long> Bigger =
        (current, baseline) => current > baseline ? BettererConstraintResult.Better
            : current < baseline ? BettererConstraintResult.Worse
            : BettererConstraintResult.Same;

    /// <summary>
    /// For tests that report a set of named items (e.g. forbidden types). Any item not present
    /// in the baseline is a regression; removing items without adding any is an improvement.
    /// </summary>
    public static BettererConstraint<List<T>> SetBased<T>(IEqualityComparer<T>? comparer = null) =>
        (current, baseline) =>
        {
            var baselineSet = new HashSet<T>(baseline, comparer);
            if (current.Any(item => !baselineSet.Contains(item)))
            {
                return BettererConstraintResult.Worse;
            }

            var currentSet = new HashSet<T>(current, comparer);
            if (baseline.Any(item => !currentSet.Contains(item)))
            {
                return BettererConstraintResult.Better;
            }

            return BettererConstraintResult.Same;
        };
}
