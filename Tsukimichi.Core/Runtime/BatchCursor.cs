namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Position in a batch of calls that is spread over ticks and may fail part-way (the Wotsit registration). A failed
/// call keeps the cursor on the same index so the next tick retries it; after <see cref="MaxAttempts"/> consecutive
/// failures on one index the cursor gives up and the caller logs and abandons the rest rather than retrying forever.
/// Holds no items itself; the caller keeps the list.
/// </summary>
public sealed class BatchCursor
{
    public const int DefaultMaxAttempts = 5;

    public BatchCursor(int count, int maxAttempts = DefaultMaxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        Count = count;
        MaxAttempts = maxAttempts;
    }

    /// <summary>Items in the batch.</summary>
    public int Count { get; }

    /// <summary>Consecutive failures on one index before <see cref="GaveUp"/>.</summary>
    public int MaxAttempts { get; }

    /// <summary>The next item to process; equals <see cref="Count"/> once every item went through.</summary>
    public int Index { get; private set; }

    /// <summary>Consecutive failed attempts at <see cref="Index"/>; reset by <see cref="Advance"/>.</summary>
    public int Attempts { get; private set; }

    /// <summary>Every item was processed.</summary>
    public bool IsDone => Index >= Count;

    /// <summary>The item at <see cref="Index"/> failed <see cref="MaxAttempts"/> times; the rest is abandoned.</summary>
    public bool GaveUp { get; private set; }

    /// <summary>The batch has nothing left to try: done or given up.</summary>
    public bool IsFinished => IsDone || GaveUp;

    /// <summary>The current item succeeded: move to the next one.</summary>
    public void Advance()
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The batch is finished.");
        }

        Index++;
        Attempts = 0;
    }

    /// <summary>
    /// The current item failed. Returns true when this failure exhausted <see cref="MaxAttempts"/> and the cursor
    /// gave up; false when the same index should be retried on the next tick.
    /// </summary>
    public bool Fail()
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The batch is finished.");
        }

        Attempts++;
        if (Attempts >= MaxAttempts)
        {
            GaveUp = true;
        }

        return GaveUp;
    }
}
