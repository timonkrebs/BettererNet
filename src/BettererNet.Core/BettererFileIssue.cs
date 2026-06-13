namespace BettererNet;

/// <summary>A single issue found within a file by a <see cref="BettererFileTest"/>.</summary>
public sealed record BettererFileIssue
{
    public required int Line { get; init; }

    public required int Column { get; init; }

    public required int Length { get; init; }

    public required string Message { get; init; }

    /// <summary>
    /// A stable, line-independent fingerprint of the issue so it can be matched across runs even
    /// when surrounding code moves it to a different line.
    /// </summary>
    public required string Hash { get; init; }
}
