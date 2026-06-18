# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-alpha] - 2026-06-14

First public alpha. Functional parity with [betterer](https://github.com/phenomnomnominal/betterer)'s
core, plus .NET-native extensions.

### Added

- **Core engine** — records an accepted baseline of issues in a single, diff-friendly,
  deterministically-sorted `.betterer.results` file: new issues fail, fixed issues ratchet the
  baseline down and lock in.
- **`betterernet` global tool** (`dotnet tool install -g BettererNet.Cli`) with `init`, `start`,
  `ci`, `watch`, `precommit`, `results`, and `merge` (a git merge driver for the results file).
- **Declarative `betterer.json`** — run regex / coverage / SARIF / dotnet-format tests with no
  compiled config; auto-detected by the CLI. Regex tests skip `bin`/`obj` by default.
- **Integrations** — Regex, Coverage (Cobertura), SARIF, dotnet-format, Roslyn (compiler
  diagnostics, analyzers, syntax queries), Roslyn MSBuild-workspace loading, and NetArchTest.
- **Reporters** — console (surfaces the specific new issues by `file:line`), GitHub Actions
  annotations, SARIF, markdown (PR comment), and a trend/history report.
- **Test-framework adapters** — xUnit, NUnit, MSTest, and TUnit.
- **Large-team features** — per-test ownership and issue budgets, goals and deadlines,
  content-based caching (`--cache`), and a nullable-reference-type adoption preset.
- **Packaging** — 13 NuGet packages with shared metadata (MIT, README, repository links).

[Unreleased]: https://github.com/timonkrebs/BettererNet/compare/v0.1.0-alpha...HEAD
[0.1.0-alpha]: https://github.com/timonkrebs/BettererNet/releases/tag/v0.1.0-alpha
