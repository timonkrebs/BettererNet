namespace BettererNet;

/// <summary>
/// Supplies the set of tests for a project. A config assembly exposes one implementation with a
/// public parameterless constructor; the CLI loads the assembly and runs the tests it returns.
/// </summary>
public interface IBettererSuiteProvider
{
    IEnumerable<IBettererTest> GetTests();
}
