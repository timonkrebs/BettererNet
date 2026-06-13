using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// A Betterer test: a function producing a result of type <typeparamref name="T"/>, a
/// <see cref="BettererConstraint{T}"/> comparing it to the baseline, and an optional goal and
/// deadline. This is the generic backbone every test type (counting, file, etc.) builds on.
/// </summary>
public sealed class BettererTest<T> : IBettererTest
{
    private readonly Func<CancellationToken, Task<T>> _test;
    private readonly BettererConstraint<T> _constraint;
    private readonly IBettererSerializer<T> _serializer;
    private readonly Func<T, bool>? _goal;
    private readonly DateTimeOffset? _deadline;

    public BettererTest(
        string name,
        Func<CancellationToken, Task<T>> test,
        BettererConstraint<T> constraint,
        IBettererSerializer<T>? serializer = null,
        Func<T, bool>? goal = null,
        DateTimeOffset? deadline = null,
        bool isSkipped = false)
    {
        Name = name;
        _test = test;
        _constraint = constraint;
        _serializer = serializer ?? JsonBettererSerializer<T>.Instance;
        _goal = goal;
        _deadline = deadline;
        IsSkipped = isSkipped;
    }

    public BettererTest(
        string name,
        Func<T> test,
        BettererConstraint<T> constraint,
        IBettererSerializer<T>? serializer = null,
        Func<T, bool>? goal = null,
        DateTimeOffset? deadline = null,
        bool isSkipped = false)
        : this(name, _ => Task.FromResult(test()), constraint, serializer, goal, deadline, isSkipped)
    {
    }

    public string Name { get; }

    public bool IsSkipped { get; }

    public async Task<BettererRunSummary> RunAsync(
        JsonNode? baselineValue,
        BettererRunContext context,
        CancellationToken cancellationToken = default)
    {
        if (IsSkipped)
        {
            return BettererRunSummary.Skipped(Name);
        }

        T current;
        try
        {
            current = await _test(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            return BettererRunSummary.Failed(Name, error);
        }

        var serialized = _serializer.Serialize(current);
        var hasBaseline = baselineValue is not null;
        var goalMet = _goal?.Invoke(current) ?? false;

        var comparison = hasBaseline
            ? _constraint(current, _serializer.Deserialize(baselineValue))
            : BettererConstraintResult.Better;

        var deadlinePassed = _deadline is { } deadline && context.Now > deadline;

        BettererRunStatus status;
        bool shouldUpdate;

        if (goalMet)
        {
            status = BettererRunStatus.Complete;
            shouldUpdate = comparison != BettererConstraintResult.Worse;
        }
        else if (deadlinePassed)
        {
            // The deadline lapsed without meeting the goal: the run fails, but progress is still kept.
            status = BettererRunStatus.Expired;
            shouldUpdate = comparison != BettererConstraintResult.Worse;
        }
        else if (!hasBaseline)
        {
            status = BettererRunStatus.New;
            shouldUpdate = true;
        }
        else
        {
            switch (comparison)
            {
                case BettererConstraintResult.Better:
                    status = BettererRunStatus.Better;
                    shouldUpdate = true;
                    break;

                case BettererConstraintResult.Same:
                    status = BettererRunStatus.Same;
                    // The result is unchanged, so the on-disk baseline is already correct; leaving
                    // it untouched keeps the results file diff-stable even if line numbers shifted.
                    shouldUpdate = false;
                    break;

                default:
                    if (context.Update)
                    {
                        status = BettererRunStatus.Updated;
                        shouldUpdate = true;
                    }
                    else
                    {
                        status = BettererRunStatus.Worse;
                        shouldUpdate = false;
                    }

                    break;
            }
        }

        return new BettererRunSummary
        {
            Name = Name,
            Status = status,
            Result = serialized,
            Baseline = baselineValue,
            ShouldUpdateResults = shouldUpdate,
        };
    }
}
