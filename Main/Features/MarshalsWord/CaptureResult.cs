namespace TAOM.Features.MarshalsWord;

internal enum CaptureResult
{
    /// <summary>Not queued: vanilla runs the order at once.</summary>
    Declined,

    /// <summary>Stored as a step: vanilla must not run it.</summary>
    Queued,

    /// <summary>A selected company already holds the most steps: the order is swallowed, because running it now would wipe the queue.</summary>
    Full,
}
