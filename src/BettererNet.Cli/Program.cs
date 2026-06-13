using BettererNet;

const string version = "0.1.0-alpha";

Console.WriteLine($"BettererNet CLI v{version}");
Console.WriteLine("Incremental improvement tracking for .NET — https://github.com/timonkrebs/BettererNet");
Console.WriteLine();

// Phase 0 skeleton: no real commands yet, but prove the Core wiring by summarising a
// results file if one is present. The init/start/ci/watch/precommit/results/merge
// commands arrive in Phase 3 (see ROADMAP.md).
var resultsPath = args.Length > 0 ? args[0] : BettererResultsFile.DefaultFileName;

if (File.Exists(resultsPath))
{
    var results = await BettererResultsFile.LoadAsync(resultsPath);
    Console.WriteLine($"Results file: {Path.GetFullPath(resultsPath)}");
    Console.WriteLine($"Tracked tests: {results.Results.Count}");
    foreach (var (name, entry) in results.Results)
    {
        Console.WriteLine($"  - {name}: {entry.Issues.Count} issue(s)");
    }
}
else
{
    Console.WriteLine($"No results file found at '{resultsPath}'.");
}

Console.WriteLine();
Console.WriteLine("Commands (init, start, ci, watch, precommit, results, merge) arrive in a later phase — see ROADMAP.md.");

return 0;
