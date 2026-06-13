using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BettererNet;

/// <summary>
/// Reader/writer for the single <c>.betterer.results</c> baseline file.
/// </summary>
/// <remarks>
/// Serialisation is deterministic — test names and issues are sorted and the JSON is
/// indented — so diffs stay small and merge-friendly. Writes are atomic (write to a
/// temporary file then move into place) so a concurrent reader never observes a
/// half-written file.
/// </remarks>
public sealed class BettererResultsFile
{
    /// <summary>The conventional file name for a Betterer results file.</summary>
    public const string DefaultFileName = ".betterer.results";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly SortedDictionary<string, BettererStoredResult> _results;

    private BettererResultsFile(string path, SortedDictionary<string, BettererStoredResult> results)
    {
        Path = path;
        _results = results;
    }

    /// <summary>Absolute or relative path of the results file on disk.</summary>
    public string Path { get; }

    /// <summary>The stored results, keyed by test name (ordinal-sorted).</summary>
    public IReadOnlyDictionary<string, BettererStoredResult> Results => _results;

    /// <summary>Load the results file, returning an empty instance if it does not yet exist.</summary>
    public static async Task<BettererResultsFile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var results = new SortedDictionary<string, BettererStoredResult>(StringComparer.Ordinal);

        if (File.Exists(path))
        {
            await using var stream = File.OpenRead(path);
            var dto = await JsonSerializer.DeserializeAsync<ResultsFileDto>(stream, SerializerOptions, cancellationToken);
            if (dto?.Results is not null)
            {
                foreach (var (name, entry) in dto.Results)
                {
                    results[name] = new BettererStoredResult
                    {
                        Timestamp = entry.Timestamp,
                        Issues = entry.Issues ?? new List<string>(),
                    };
                }
            }
        }

        return new BettererResultsFile(path, results);
    }

    /// <summary>Get the stored baseline for a test, if one exists.</summary>
    public bool TryGet(string testName, [NotNullWhen(true)] out BettererStoredResult? result)
        => _results.TryGetValue(testName, out result);

    /// <summary>Add or replace the stored baseline for a test.</summary>
    public void Set(string testName, BettererStoredResult result)
        => _results[testName] = result;

    /// <summary>Remove the stored baseline for a test. Returns <c>true</c> if one was removed.</summary>
    public bool Remove(string testName)
        => _results.Remove(testName);

    /// <summary>Persist the results to disk atomically with deterministic ordering.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Nothing to store: remove the file so a clean codebase carries no baseline.
        if (_results.Count == 0)
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            return;
        }

        var dto = new ResultsFileDto { Version = CurrentVersion };
        foreach (var (name, entry) in _results)
        {
            var issues = new List<string>(entry.Issues);
            issues.Sort(StringComparer.Ordinal);
            dto.Results[name] = new ResultEntryDto { Timestamp = entry.Timestamp, Issues = issues };
        }

        var tempPath = Path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, Path, overwrite: true);
    }

    private sealed class ResultsFileDto
    {
        public int Version { get; set; } = CurrentVersion;

        public SortedDictionary<string, ResultEntryDto> Results { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ResultEntryDto
    {
        public DateTimeOffset Timestamp { get; set; }

        public List<string>? Issues { get; set; }
    }
}
