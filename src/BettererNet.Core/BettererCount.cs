using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// The generic "size" of a serialized test result — the number of issues/items it reports — used by
/// budgets and the trend report. A JSON array counts its length, an object sums its children (so a
/// file → issues map totals every issue across files), and a bare number counts as itself.
/// </summary>
public static class BettererCount
{
    public static long Of(JsonNode? node) => node switch
    {
        JsonArray array => array.Count,
        JsonObject obj => obj.Sum(pair => Of(pair.Value)),
        JsonValue value when value.TryGetValue<long>(out var number) => number,
        _ => 0,
    };
}
