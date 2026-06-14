using System.Text.Json;
using System.Text.Json.Nodes;

namespace BettererNet.Cli;

/// <summary>
/// Writes the current file-test issues as a SARIF 2.1.0 report — the inverse of
/// <c>BettererSarifTest</c> — so results can be uploaded to GitHub Code Scanning or other tools.
/// </summary>
public sealed class BettererSarifReporter : IBettererReporter
{
    private readonly string _path;
    private readonly List<BettererRunSummary> _runs = new();

    public BettererSarifReporter(string path) => _path = path;

    public void ReportRun(BettererRunSummary run) => _runs.Add(run);

    public void ReportSuite(BettererSuiteSummary suite)
    {
        var results = new JsonArray();
        var ruleIds = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var run in _runs)
        {
            if (run.Result is not JsonObject)
            {
                continue; // only file tests carry per-issue locations
            }

            var issues = BettererFileIssuesSerializer.Instance.Deserialize(run.Result);
            foreach (var (file, fileIssues) in issues.Files)
            {
                foreach (var issue in fileIssues)
                {
                    ruleIds.Add(run.Name);
                    results.Add(new JsonObject
                    {
                        ["ruleId"] = run.Name,
                        ["level"] = "warning",
                        ["message"] = new JsonObject { ["text"] = issue.Message },
                        ["locations"] = new JsonArray(new JsonObject
                        {
                            ["physicalLocation"] = new JsonObject
                            {
                                ["artifactLocation"] = new JsonObject { ["uri"] = file },
                                ["region"] = new JsonObject { ["startLine"] = issue.Line, ["startColumn"] = issue.Column },
                            },
                        }),
                    });
                }
            }
        }

        var rules = new JsonArray();
        foreach (var id in ruleIds)
        {
            rules.Add(new JsonObject { ["id"] = id });
        }

        var sarif = new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray(new JsonObject
            {
                ["tool"] = new JsonObject { ["driver"] = new JsonObject { ["name"] = "BettererNet", ["rules"] = rules } },
                ["results"] = results,
            }),
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, sarif.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>Fans a run out to several reporters (e.g. console plus a SARIF file).</summary>
public sealed class BettererCompositeReporter : IBettererReporter
{
    private readonly IReadOnlyList<IBettererReporter> _reporters;

    public BettererCompositeReporter(params IBettererReporter[] reporters) => _reporters = reporters;

    public void ReportRun(BettererRunSummary run)
    {
        foreach (var reporter in _reporters)
        {
            reporter.ReportRun(run);
        }
    }

    public void ReportSuite(BettererSuiteSummary suite)
    {
        foreach (var reporter in _reporters)
        {
            reporter.ReportSuite(suite);
        }
    }
}
