namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// One stack of the player's prison roster, projected free of TaleWorlds types so the
/// deliver-captives rule lives in <see cref="ILotrIssueService"/> and stays unit-testable (ADR-007).
/// Troop type is deliberately absent: any non-hero prisoner is deliverable.
/// </summary>
public readonly struct LotrCaptiveStack
{
    public LotrCaptiveStack(bool isHero, int number)
    {
        IsHero = isHero;
        Number = number;
    }

    /// <summary>Heroes are never deliverable — removing one by raw roster edits skips EndCaptivityAction.</summary>
    public bool IsHero { get; }

    /// <summary>Troops in this stack, wounded included.</summary>
    public int Number { get; }
}
