using System.Text.Json;
using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>Converts a test's result value to and from the JSON stored in the results file.</summary>
public interface IBettererSerializer<T>
{
    JsonNode? Serialize(T value);

    T Deserialize(JsonNode? value);
}

/// <summary>Default <see cref="IBettererSerializer{T}"/> backed by <c>System.Text.Json</c>.</summary>
public sealed class JsonBettererSerializer<T> : IBettererSerializer<T>
{
    /// <summary>Shared, stateless instance.</summary>
    public static readonly JsonBettererSerializer<T> Instance = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public JsonNode? Serialize(T value) => JsonSerializer.SerializeToNode(value, Options);

    public T Deserialize(JsonNode? value) =>
        value is null
            ? throw new InvalidOperationException("Cannot deserialize a null result value.")
            : value.Deserialize<T>(Options)!;
}
