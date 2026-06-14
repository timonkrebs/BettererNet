using Microsoft.CodeAnalysis;

namespace BettererNet;

/// <summary>
/// Turnkey preset for incrementally adopting nullable reference types: a <see cref="BettererProjectTest"/>
/// over a real project, filtered to nullable warnings, with a goal of zero. Enable
/// <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c> in the project, baseline the existing warnings, then
/// burn them down.
/// </summary>
public static class BettererNullableTest
{
    /// <summary>Track the project's nullable warnings (defaults to a goal of zero — fully adopted).</summary>
    public static BettererTest<BettererFileIssues> Create(
        string name,
        string projectPath,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null) =>
        BettererProjectTest.FromProject(name, projectPath, IsNullableWarning, goal: goal ?? BettererFileTest.NoIssues, deadline: deadline);

    /// <summary>Nullable reference-type warnings are the <c>CSxxxx</c> diagnostics numbered 8600 and above.</summary>
    public static bool IsNullableWarning(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Warning
        && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal)
        && int.TryParse(diagnostic.Id.AsSpan(2), out var number)
        && number >= 8600;
}
