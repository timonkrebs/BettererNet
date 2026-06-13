namespace BettererNet;

/// <summary>
/// The set of issues a Betterer test currently reports.
/// </summary>
/// <remarks>
/// Phase 1 will generalise this into a richer file/issue model that carries line and
/// column information and supports hashing so issues survive code moving around.
/// </remarks>
public sealed class BettererResult
{
    /// <summary>The names of the failing types/members reported by the test.</summary>
    public List<string> FailingTypeNames { get; set; } = new();
}
