using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BettererNet.Cli;

/// <summary>The CLI commands, built on <see cref="BettererRunner"/>. Each returns a process exit code.</summary>
public static class BettererCli
{
    /// <summary>Parse arguments and dispatch to a command. Unknown options return a non-zero code.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IEnumerable<IBettererTest> tests, CancellationToken cancellationToken = default)
    {
        var (command, options, error) = Parse(args);
        if (error is not null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        return command switch
        {
            "start" => await StartAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "ci" => await CiAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "precommit" => await PrecommitAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "watch" => await WatchAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "results" => await ResultsAsync(options, cancellationToken).ConfigureAwait(false),
            _ => Unknown(command),
        };
    }

    /// <summary>Run tests, record improvements, and fail if anything got worse.</summary>
    public static async Task<int> StartAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var reporter = ResolveReporter(options);
        var selected = Filter(tests, options.Filters);
        var resultsFile = await BettererResultsFile.LoadAsync(options.ResultsPath, cancellationToken).ConfigureAwait(false);
        var context = new BettererRunContext { Update = options.Update };

        var summary = await BettererRunner.RunAsync(selected, resultsFile, context, write: true, cancellationToken).ConfigureAwait(false);
        Report(reporter, summary);
        return summary.IsFailure ? 1 : 0;
    }

    /// <summary>Run tests without writing and fail if the results file is out of date or regressed.</summary>
    public static async Task<int> CiAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var reporter = ResolveReporter(options);
        var selected = Filter(tests, options.Filters);
        var resultsFile = await BettererResultsFile.LoadAsync(options.ResultsPath, cancellationToken).ConfigureAwait(false);

        var summary = await BettererRunner.RunAsync(selected, resultsFile, new BettererRunContext(), write: false, cancellationToken).ConfigureAwait(false);
        Report(reporter, summary);

        // CI fails if running would change the results file (a missing commit) or a test regressed.
        var hasDiff = summary.Runs.Any(run => run.Status is not (BettererRunStatus.Same or BettererRunStatus.Skipped));
        return hasDiff ? 1 : 0;
    }

    /// <summary>Run like <see cref="StartAsync"/>, then <c>git add</c> the results file when it passes.</summary>
    public static async Task<int> PrecommitAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var code = await StartAsync(tests, options, cancellationToken).ConfigureAwait(false);
        if (code == 0 && File.Exists(options.ResultsPath))
        {
            TryGitAdd(options.ResultsPath);
        }

        return code;
    }

    /// <summary>Print the contents of the results file.</summary>
    public static async Task<int> ResultsAsync(BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var resultsFile = await BettererResultsFile.LoadAsync(options.ResultsPath, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Results file: {Path.GetFullPath(options.ResultsPath)}");
        Console.WriteLine($"Tracked tests: {resultsFile.Results.Count}");
        foreach (var (name, value) in resultsFile.Results)
        {
            Console.WriteLine($"  - {name}: {Describe(value)}");
        }

        return 0;
    }

    /// <summary>Run once, then re-run whenever a <c>.cs</c> file under the current directory changes.</summary>
    public static async Task<int> WatchAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var testList = tests.ToList();
        await StartAsync(testList, options, cancellationToken).ConfigureAwait(false);

        using var watcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
        {
            IncludeSubdirectories = true,
            Filter = "*.cs",
            EnableRaisingEvents = true,
        };

        using var signal = new SemaphoreSlim(0);
        void OnChange(object _, FileSystemEventArgs __) => signal.Release();
        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += (_, _) => signal.Release();

        Console.WriteLine("Watching for changes (Ctrl+C to stop)...");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false); // debounce a burst of edits
                while (signal.CurrentCount > 0)
                {
                    await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                await StartAsync(testList, options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }

    /// <summary>Parse args into a command name and options. Returns an error message for bad input.</summary>
    public static (string Command, BettererCliOptions Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var command = "start";
        var commandSet = false;
        var results = BettererResultsFile.DefaultFileName;
        var filters = new List<string>();
        bool update = false, strict = false, silent = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-r" or "--results":
                    if (++i >= args.Count)
                    {
                        return (command, new BettererCliOptions(), "Missing value for --results.");
                    }

                    results = args[i];
                    break;

                case "-f" or "--filter":
                    if (++i >= args.Count)
                    {
                        return (command, new BettererCliOptions(), "Missing value for --filter.");
                    }

                    filters.Add(args[i]);
                    break;

                case "-u" or "--update":
                    update = true;
                    break;

                case "--strict":
                    strict = true;
                    break;

                case "-s" or "--silent":
                    silent = true;
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        return (command, new BettererCliOptions(), $"Unknown option '{arg}'.");
                    }

                    if (commandSet)
                    {
                        return (command, new BettererCliOptions(), $"Unexpected argument '{arg}'.");
                    }

                    command = arg;
                    commandSet = true;
                    break;
            }
        }

        var options = new BettererCliOptions
        {
            ResultsPath = results,
            Filters = filters,
            Update = update,
            Strict = strict,
            Silent = silent,
        };
        return (command, options, null);
    }

    private static IEnumerable<IBettererTest> Filter(IEnumerable<IBettererTest> tests, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0)
        {
            return tests;
        }

        var positives = filters.Where(filter => !filter.StartsWith('!')).ToList();
        var negatives = filters.Where(filter => filter.StartsWith('!')).Select(filter => filter[1..]).ToList();

        return tests.Where(test =>
            !negatives.Any(pattern => Regex.IsMatch(test.Name, pattern)) &&
            (positives.Count == 0 || positives.Any(pattern => Regex.IsMatch(test.Name, pattern))));
    }

    private static IBettererReporter ResolveReporter(BettererCliOptions options) =>
        options.Reporter ?? (options.Silent ? new BettererSilentReporter() : new BettererConsoleReporter());

    private static void Report(IBettererReporter reporter, BettererSuiteSummary summary)
    {
        foreach (var run in summary.Runs)
        {
            reporter.ReportRun(run);
        }

        reporter.ReportSuite(summary);
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'. Expected: init, start, ci, watch, precommit, results.");
        return 2;
    }

    private static string Describe(JsonNode value) => value switch
    {
        JsonArray array => $"{array.Count} issue(s)",
        JsonObject obj => $"{obj.Sum(pair => pair.Value is JsonArray array ? array.Count : 0)} issue(s) across {obj.Count} file(s)",
        _ => value.ToJsonString(),
    };

    private static void TryGitAdd(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", $"add \"{path}\"") { UseShellExecute = false });
            process?.WaitForExit();
        }
        catch
        {
            // Best effort: precommit still succeeds even if git isn't available.
        }
    }
}
