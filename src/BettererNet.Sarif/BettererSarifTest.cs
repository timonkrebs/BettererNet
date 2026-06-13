using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Factory for tests backed by a SARIF report (the standard static-analysis output format, emitted
/// by Roslyn analyzers via <c>-warnaserror</c>/<c>--report</c>, .NET format, and many other tools).
/// Importing SARIF turns any analyzer's findings into a Betterer file test.
/// </summary>
public static class BettererSarifTest
{
    private static readonly HashSet<string> DefaultLevels = new(StringComparer.OrdinalIgnoreCase) { "warning", "error" };

    /// <summary>Create a test from a SARIF (2.1.0) report. Results are kept when their level is in <paramref name="levels"/>.</summary>
    public static BettererTest<BettererFileIssues> Create(
        string name,
        string sarifReportPath,
        ISet<string>? levels = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        var included = levels ?? DefaultLevels;
        return BettererFileTest.Create(name, () => Parse(sarifReportPath, included), goal, deadline);
    }

    private static BettererFileIssues Parse(string reportPath, ISet<string> levels)
    {
        var issues = new BettererFileIssues();
        if (JsonNode.Parse(File.ReadAllText(reportPath)) is not JsonObject root || root["runs"] is not JsonArray runs)
        {
            return issues;
        }

        foreach (var run in runs.OfType<JsonObject>())
        {
            if (run["results"] is not JsonArray results)
            {
                continue;
            }

            foreach (var result in results.OfType<JsonObject>())
            {
                // SARIF omits `level` when it matches the rule's default; treat that as a warning.
                var level = result["level"]?.GetValue<string>() ?? "warning";
                if (!levels.Contains(level))
                {
                    continue;
                }

                var physical = (result["locations"] as JsonArray)?
                    .OfType<JsonObject>().FirstOrDefault()?["physicalLocation"] as JsonObject;
                var uri = (physical?["artifactLocation"] as JsonObject)?["uri"]?.GetValue<string>();
                if (string.IsNullOrEmpty(uri))
                {
                    continue; // results without a file location can't be tracked per file
                }

                var region = physical?["region"] as JsonObject;
                // SARIF positions are 1-based; clamp so a missing/malformed region never yields line 0.
                var startLine = Math.Max(1, region?["startLine"]?.GetValue<int>() ?? 1);
                var startColumn = Math.Max(1, region?["startColumn"]?.GetValue<int>() ?? 1);
                var endColumn = region?["endColumn"]?.GetValue<int>();
                var length = endColumn is int end && end > startColumn ? end - startColumn : 0;

                var ruleId = result["ruleId"]?.GetValue<string>();
                var text = result["message"]?["text"]?.GetValue<string>() ?? string.Empty;
                var message = string.IsNullOrEmpty(ruleId) ? text : $"{ruleId}: {text}";

                issues.Add(NormalizePath(uri), startLine, startColumn, length, message);
            }
        }

        return issues;
    }

    private static string NormalizePath(string uri)
    {
        // SARIF artifactLocation.uri is a URI, often emitted as file:///abs/path. Convert file URIs
        // to a local path so keys line up with repo-relative file paths; leave relative URIs as-is.
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
        {
            return parsed.LocalPath.Replace('\\', '/');
        }

        return uri.Replace('\\', '/');
    }
}
