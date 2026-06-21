namespace BettererNet;

/// <summary>
/// The set of issues a Betterer test currently reports.
/// Used with <see cref="BettererAssertions.AssertAsync(string,string,BettererResult,bool,Action{string})"/>.
/// </summary>
public sealed class BettererResult
{
    /// <summary>The names of the failing types/members reported by the test.</summary>
    public List<string> FailingTypeNames { get; set; } = new();
}
