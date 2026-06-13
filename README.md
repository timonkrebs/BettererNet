# BettererNet

Allows incremental improvements of .NET architecture for large teams, or a legacy codebase.
Inspired by [betterer](https://github.com/phenomnomnominal/betterer) but for .NET.

> **Status:** early development. **Phase 0 (Foundation) is complete** — see [ROADMAP.md](ROADMAP.md)
> for the full plan towards feature parity with `betterer` and the .NET-native extensions on top.

## How it works

Write a test that reports the current set of issues in your codebase (architecture
violations, lint findings, etc.). BettererNet records an accepted **baseline** and then:

- **New issues fail the test** — regressions are caught immediately.
- **Fixed issues ratchet the baseline down** — improvements are locked in automatically and
  cannot be undone.
- The baseline lives in a single, diff-friendly, deterministically-sorted
  `.betterer.results` file that you commit to source control.

## Repository layout

```
src/
  BettererNet.Core    # engine: results-file reader/writer, result model
  BettererNet.Xunit   # xUnit adapter — assert against the baseline from your tests
  BettererNet.Cli     # `dotnet betterernet` tool (skeleton; commands land in Phase 3)
samples/
  SampleProject       # a project with intentional issues
  SampleTest          # example Betterer tests (NetArchTest-based)
tools/
  InspectCodeSnapshot # legacy ReSharper-snapshot prototype (folded into Roslyn integration later)
tests/
  BettererNet.Tests   # unit tests for the core + xUnit adapter
```

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer.

## Build & test

```bash
dotnet build BettererNet.sln
dotnet test  BettererNet.sln
```

## Usage (xUnit)

Reference `BettererNet.Xunit` from your test project, then assert against the baseline:

```csharp
using BettererNet;
using NetArchTest.Rules;
using Xunit;

public class ArchitectureTests
{
    [Fact]
    public async Task InterfacesShouldStartWithI()
    {
        var result = Types.InAssembly(typeof(MyMarker).Assembly)
            .That().AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .GetResult();

        // result.FailingTypeNames -> BettererResult
        var betterer = new BettererResult();
        betterer.FailingTypeNames.AddRange(result.FailingTypeNames);

        await new Betterer().AssertAsync(betterer);
    }
}
```

`new Betterer()` keys the result by the calling test method name and stores it in
`.betterer.results` in the test project directory. Pass an explicit `testName` or
`resultsPath` to the constructor to override either.

### Seeding the first baseline

By default, a test that reports issues with **no existing baseline fails** — accepting a new
baseline is meant to be an explicit choice. Set the `BETTERER_UPDATE` environment variable to
record the current results as the baseline (it also accepts regressions, like betterer's
`--update`):

```bash
BETTERER_UPDATE=1 dotnet test
```

Commit the generated `.betterer.results`. From then on, run `dotnet test` normally: it passes
while issues stay the same or shrink, and fails when new issues appear.

### Other test types

The same adapter runs any engine test, so you can also assert counting and file tests:

```csharp
// Counting test: keep a number from growing (e.g. compiler warnings), aiming for zero.
await new Betterer().AssertAsync(
    BettererCountTest.Create("CompilerWarnings", CountWarnings, goal: 0));

// File test: track issues per file (line/column/length/message), with a stable,
// line-independent hash so issues survive code moving around.
await new Betterer().AssertAsync(BettererFileTest.Create("Analyzer", () =>
{
    var issues = new BettererFileIssues();
    issues.Add("Foo.cs", line: 10, column: 5, length: 3, message: "CA1822: mark member static");
    return issues;
}));
```

### Built-in test types

`BettererNet.Regex` and `BettererNet.Roslyn` provide ready-made file tests:

```csharp
// Regex: ban a pattern and burn down existing matches.
await new Betterer().AssertAsync(
    BettererRegexTest.Create("NoConsoleWriteLine", @"Console\.WriteLine", new[] { "**/*.cs" }));

// Roslyn compiler diagnostics: adopt nullable reference types incrementally.
await new Betterer().AssertAsync(BettererRoslynTest.Diagnostics(
    "Nullable", sourceFiles,
    filter: d => d.Id.StartsWith("CS8"),
    compilationOptions: new CSharpCompilationOptions(
        OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)));

// Roslyn syntax query (the tsquery analog): count nodes you want to eliminate.
await new Betterer().AssertAsync(BettererRoslynTest.SyntaxQuery(
    "NoGoto", sourceFiles, node => node is GotoStatementSyntax));

// Roslyn analyzers (the eslint analog): track any DiagnosticAnalyzer's findings.
await new Betterer().AssertAsync(BettererRoslynTest.Analyzers("Style", sourceFiles, analyzers));

// Coverage: track uncovered lines from a Cobertura report, aiming for full coverage.
await new Betterer().AssertAsync(
    BettererCoverageTest.Create("Coverage", "coverage.cobertura.xml", goal: BettererFileTest.NoIssues));

// Architecture (NetArchTest): baseline existing violations, fail on new ones.
await new Betterer().AssertAsync(BettererArchTest.Create("Layering", () =>
    Types.InAssembly(assembly).That().ResideInNamespace("App.Domain")
        .ShouldNot().HaveDependencyOn("App.Infrastructure").GetResult()));
```

## CLI

The `betterernet` tool (`BettererNet.Cli`) runs a suite defined in a **compiled config assembly** —
a class library that implements `IBettererSuiteProvider`:

```csharp
public sealed class BettererConfig : IBettererSuiteProvider
{
    public IEnumerable<IBettererTest> GetTests()
    {
        yield return BettererRegexTest.Create("NoTodos", "TODO", new[] { "**/*.cs" });
    }
}
```

Build it, then point the tool at the assembly:

```bash
betterernet --config path/to/MyConfig.dll start     # run; record improvements, fail on regressions
betterernet --config path/to/MyConfig.dll ci        # fail if the results file is out of date
betterernet --config path/to/MyConfig.dll watch     # re-run on .cs changes
betterernet --config path/to/MyConfig.dll precommit # run, then `git add` the results
betterernet results                                 # print the current results file
betterernet init                                    # scaffold a starter BettererConfig.cs
```

Common options: `--results <path>`, `--filter <regex>` (repeatable; a leading `!` negates),
`--update` (accept regressions), `--silent`. The `merge` command arrives in Phase 4 — see
[ROADMAP.md](ROADMAP.md).
