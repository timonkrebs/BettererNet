using System.Xml;
using System.Xml.Linq;

namespace BettererNet;

/// <summary>
/// Factory for coverage tests backed by a Cobertura XML report (e.g. produced by coverlet via
/// <c>dotnet test --collect:"XPlat Code Coverage"</c>). Each uncovered line becomes a file issue,
/// so the test fails when coverage drops and ratchets up as lines become covered.
/// </summary>
public static class BettererCoverageTest
{
    /// <summary>Create a coverage test from a Cobertura report path. Goal of zero = full coverage.</summary>
    public static BettererTest<BettererFileIssues> Create(
        string name,
        string coberturaReportPath,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null) =>
        BettererFileTest.Create(
            name,
            () => Parse(coberturaReportPath),
            goal,
            deadline,
            fingerprint: () => BettererFileFingerprint.Compute(new[] { coberturaReportPath }));

    private static BettererFileIssues Parse(string reportPath)
    {
        var issues = new BettererFileIssues();

        // Parse the (potentially untrusted) report without resolving external entities or
        // expanding a DTD, guarding against XXE and entity-expansion (billion-laughs) attacks.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
        };
        using var reader = XmlReader.Create(reportPath, settings);
        var document = XDocument.Load(reader);

        // A source file can be split across multiple <class> elements (partial classes), so
        // dedupe by (file, line) to avoid counting an uncovered line more than once.
        var seen = new HashSet<(string File, int Line)>();

        foreach (var classElement in document.Descendants("class"))
        {
            var filename = (string?)classElement.Attribute("filename");
            if (string.IsNullOrEmpty(filename))
            {
                continue;
            }

            var path = filename.Replace('\\', '/');
            var lines = classElement.Element("lines");
            if (lines is null)
            {
                continue;
            }

            foreach (var line in lines.Elements("line"))
            {
                if (((int?)line.Attribute("hits") ?? 0) != 0)
                {
                    continue;
                }

                var number = (int?)line.Attribute("number") ?? 0;
                if (seen.Add((path, number)))
                {
                    issues.Add(path, number, column: 1, length: 0, message: "Uncovered line");
                }
            }
        }

        return issues;
    }
}
