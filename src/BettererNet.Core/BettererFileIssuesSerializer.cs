using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Serializes <see cref="BettererFileIssues"/> to the shape <c>{ "file": [ { line, column,
/// length, message, hash }, ... ] }</c>. The stored issue hash is preserved on load.
/// </summary>
public sealed class BettererFileIssuesSerializer : IBettererSerializer<BettererFileIssues>
{
    public static readonly BettererFileIssuesSerializer Instance = new();

    public JsonNode? Serialize(BettererFileIssues value)
    {
        var root = new JsonObject();
        foreach (var (file, issues) in value.Files)
        {
            var array = new JsonArray();
            foreach (var issue in issues)
            {
                array.Add(new JsonObject
                {
                    ["line"] = issue.Line,
                    ["column"] = issue.Column,
                    ["length"] = issue.Length,
                    ["message"] = issue.Message,
                    ["hash"] = issue.Hash,
                });
            }

            root[file] = array;
        }

        return root;
    }

    public BettererFileIssues Deserialize(JsonNode? value)
    {
        var result = new BettererFileIssues();
        if (value is JsonObject root)
        {
            foreach (var (file, node) in root)
            {
                if (node is not JsonArray array)
                {
                    continue;
                }

                foreach (var item in array)
                {
                    if (item is not JsonObject issue)
                    {
                        continue;
                    }

                    result.AddExisting(file, new BettererFileIssue
                    {
                        Line = issue["line"]?.GetValue<int>() ?? 0,
                        Column = issue["column"]?.GetValue<int>() ?? 0,
                        Length = issue["length"]?.GetValue<int>() ?? 0,
                        Message = issue["message"]?.GetValue<string>() ?? string.Empty,
                        Hash = issue["hash"]?.GetValue<string>() ?? string.Empty,
                    });
                }
            }
        }

        return result;
    }
}
