using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Factory for tests backed by a <c>dotnet format</c> report. Run
/// <c>dotnet format --verify-no-changes --report format-report.json</c> in CI (it lists every
/// whitespace, import-ordering, and analyzer/style fix it would make without applying them), then
/// ingest the report here to baseline existing formatting debt and burn it down incrementally —
/// the .NET analog of adopting a strict lint config one rule at a time.
/// </summary>
public static class BettererFormatTest
{
    /// <summary>
    /// Create a test from a <c>dotnet format</c> JSON report. When <paramref name="diagnostics"/> is
    /// given, only changes whose <c>DiagnosticId</c> is in the set are tracked (e.g. just
    /// <c>WHITESPACE</c>, or a specific <c>IDExxxx</c> rule); otherwise every reported change counts.
    /// </summary>
    public static BettererTest<BettererFileIssues> Create(
        string name,
        string formatReportPath,
        ISet<string>? diagnostics = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null) =>
        BettererFileTest.Create(
            name,
            () => Parse(formatReportPath, diagnostics),
            goal,
            deadline,
            fingerprint: () => BettererFileFingerprint.Compute(new[] { formatReportPath }));

    private static BettererFileIssues Parse(string reportPath, ISet<string>? diagnostics)
    {
        var issues = new BettererFileIssues();
        if (JsonNode.Parse(File.ReadAllText(reportPath)) is not JsonArray documents)
        {
            return issues;
        }

        foreach (var document in documents.OfType<JsonObject>())
        {
            // Prefer the absolute path; fall back to the file name so an issue is never dropped.
            var path = document["FilePath"]?.GetValue<string>() ?? document["FileName"]?.GetValue<string>();
            if (string.IsNullOrEmpty(path) || document["FileChanges"] is not JsonArray changes)
            {
                continue;
            }

            foreach (var change in changes.OfType<JsonObject>())
            {
                var diagnosticId = change["DiagnosticId"]?.GetValue<string>() ?? string.Empty;
                if (diagnostics is not null && !diagnostics.Contains(diagnosticId))
                {
                    continue;
                }

                // dotnet format positions are 1-based; clamp so a missing field never yields line 0.
                var line = Math.Max(1, change["LineNumber"]?.GetValue<int>() ?? 1);
                var column = Math.Max(1, change["CharNumber"]?.GetValue<int>() ?? 1);
                var description = change["FormatDescription"]?.GetValue<string>() ?? string.Empty;
                var message = string.IsNullOrEmpty(diagnosticId) ? description : $"{diagnosticId}: {description}";

                issues.Add(path.Replace('\\', '/'), line, column, length: 0, message);
            }
        }

        return issues;
    }
}
