using Xunit;

namespace BettererNet.Tests;

public sealed class BettererConstraintsTests
{
    [Fact]
    public void Smaller_FewerIsBetter()
    {
        Assert.Equal(BettererConstraintResult.Better, BettererConstraints.Smaller(5, 10));
        Assert.Equal(BettererConstraintResult.Same, BettererConstraints.Smaller(10, 10));
        Assert.Equal(BettererConstraintResult.Worse, BettererConstraints.Smaller(15, 10));
    }

    [Fact]
    public void Bigger_MoreIsBetter()
    {
        Assert.Equal(BettererConstraintResult.Better, BettererConstraints.Bigger(15, 10));
        Assert.Equal(BettererConstraintResult.Same, BettererConstraints.Bigger(10, 10));
        Assert.Equal(BettererConstraintResult.Worse, BettererConstraints.Bigger(5, 10));
    }

    [Fact]
    public void SetBased_DetectsRegressionImprovementAndSame()
    {
        var constraint = BettererConstraints.SetBased<string>();

        Assert.Equal(BettererConstraintResult.Worse, constraint(new() { "a", "b" }, new() { "a" }));
        Assert.Equal(BettererConstraintResult.Better, constraint(new() { "a" }, new() { "a", "b" }));
        Assert.Equal(BettererConstraintResult.Same, constraint(new() { "b", "a" }, new() { "a", "b" }));
        // A swap (one removed, one added) still counts as a regression.
        Assert.Equal(BettererConstraintResult.Worse, constraint(new() { "a", "c" }, new() { "a", "b" }));
    }
}
