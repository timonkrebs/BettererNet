# BettererNet

[![CI](https://github.com/timonkrebs/BettererNet/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/timonkrebs/BettererNet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/BettererNet.Cli.svg?label=BettererNet.Cli)](https://www.nuget.org/packages/BettererNet.Cli)

Allows incremental improvements of .NET architecture for large teams, or a legacy codebase.
Inspired by [betterer](https://github.com/phenomnomnominal/betterer) but for .NET.

> **Status:** functional parity with `betterer`'s core, plus .NET-native extensions. See
> **[DOCUMENTATION.md](DOCUMENTATION.md)** for the full user guide, and [ROADMAP.md](ROADMAP.md) for
> the plan and what's implemented.

## How it works

Write a test that reports the current set of issues in your codebase (architecture
violations, lint findings, etc.). BettererNet records an accepted **baseline** and then:

- **New issues fail the test** — regressions are caught immediately.
- **Fixed issues ratchet the baseline down** — improvements are locked in automatically and
  cannot be undone.
- The baseline lives in a single, diff-friendly, deterministically-sorted
  `.betterer.results` file that you commit to source control.

**BettererNet uses BettererNet:** this repo's CI runs `betterernet ci` against its own committed
baseline (see [`betterer.json`](betterer.json)), so new `#pragma warning disable`, `async void`,
`NotImplementedException`, or sync-over-async calls fail the build.

## Repository layout

```
src/
  BettererNet.Core    # engine: results-file reader/writer, result model
  BettererNet.Xunit   # xUnit adapter — assert against the baseline from your tests
  BettererNet.Cli     # `betterernet` global tool — ci/start/watch/precommit, betterer.json
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

// Nullable-adoption preset (BettererNet.Roslyn.MSBuild): the turnkey "enable #nullable and
// burn the warnings down" recipe over a real project — goal defaults to zero.
await new Betterer().AssertAsync(BettererNullableTest.Create("Nullable", "src/App/App.csproj"));

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

// SARIF: import any analyzer's report (the whole SARIF ecosystem) as a file test.
await new Betterer().AssertAsync(BettererSarifTest.Create("Analyzers", "analysis.sarif"));

// dotnet format: adopt strict formatting/style incrementally. Run
// `dotnet format --verify-no-changes --report format-report.json` in CI, then track its findings.
await new Betterer().AssertAsync(BettererFormatTest.Create("Format", "format-report.json"));
```

## CLI

Install the tool, then define a suite — declaratively or in code:

```bash
dotnet tool install --global BettererNet.Cli   # run as `betterernet`
```

**Declarative `betterer.json`** (no code; covers the data-driven tests — regex, coverage, SARIF, dotnet-format):

```json
{
  "results": ".betterer.results",
  "tests": {
    "NoTodos":   { "type": "regex",    "pattern": "TODO", "includes": ["**/*.cs"] },
    "Coverage":  { "type": "coverage", "report": "coverage.cobertura.xml", "goalZero": true },
    "Analyzers": { "type": "sarif",    "report": "analysis.sarif", "levels": ["error"],
                   "owner": "@platform-team", "budget": 50 }
  }
}
```

Any test can carry an **`owner`** (a person/team, surfaced in reports so debt routes to the right
place) and a **`budget`** (a hard ceiling on the issue count — a run over budget fails even if it
improved on its baseline, and is never recorded above the ceiling). In code, wrap any test with
`.WithOwnership(owner, budget)`.

`betterernet ci` auto-detects `betterer.json` in the working directory. Tests that need code (Roslyn
syntax queries, NetArchTest rules) use a **compiled config assembly** — a class library implementing
`IBettererSuiteProvider`, passed with `--config My.dll`.

```bash
betterernet start      # run; record improvements, fail on regressions
betterernet ci         # fail if the results file is out of date or regressed (no write)
betterernet watch      # re-run on .cs changes
betterernet precommit  # run, then `git add` the results
betterernet results    # print the current results file
betterernet init       # scaffold a starter betterer.json (--automerge also sets up the git merge driver)
betterernet merge <base> <ours> <theirs>   # resolve a .betterer.results conflict
```

Common options: `--results <path>`, `--filter <regex>` (repeatable; a leading `!` negates),
`--update` (accept regressions), `--workers <n>` (parallelism),
`--reporter <console|github|silent>` (the `github` reporter emits CI annotations + a step-summary
table), `--sarif <path>` (also write a SARIF report of current issues), `--markdown <path>` (run
summary to post as a PR comment), `--history <path>` (append a snapshot + render a trend),
`--cache` (skip tests with unchanged inputs), `--silent`.

> Prefer NUnit, MSTest, or TUnit? `BettererNet.NUnit`, `BettererNet.MSTest`, and `BettererNet.TUnit` expose the same `new Betterer().AssertAsync(...)` API.

With `--automerge` configured, git resolves `.betterer.results` conflicts automatically by taking
the tightest baseline (so no branch's improvements are lost).
