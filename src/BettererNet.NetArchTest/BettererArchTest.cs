using NetArchTest.Rules;

namespace BettererNet;

/// <summary>
/// Factory for architecture tests built on NetArchTest. The failing type names of a rule become the
/// tracked issues, so existing violations are baselined and only new ones fail the test.
/// </summary>
public static class BettererArchTest
{
    /// <summary>Wrap a NetArchTest rule (a function returning its <see cref="TestResult"/>) as a Betterer test.</summary>
    public static BettererTest<List<string>> Create(
        string name,
        Func<TestResult> rule,
        Func<List<string>, bool>? goal = null,
        DateTimeOffset? deadline = null) =>
        new(
            name,
            () => FailingTypeNames(rule()),
            BettererConstraints.SetBased<string>(),
            goal: goal,
            deadline: deadline);

    private static List<string> FailingTypeNames(TestResult result) =>
        result.FailingTypeNames is { } names ? [.. names] : [];
}
