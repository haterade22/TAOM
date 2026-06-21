using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Wraps the sealed <c>Hero</c> being polled for an issue, exposing only what the pure
/// <c>ILotrIssueService</c> needs to decide eligibility (ADR-007). The occupation mapping
/// (vanilla <c>Occupation</c> → <see cref="IssueGiverOccupation"/>) happens in the implementation;
/// <see cref="Occupation"/> is null when the hero matches no issue-giving role.
/// </summary>
public interface ILotrIssueGiverAdapter
{
    bool IsValid { get; }

    /// <summary>The issue-giving role this hero fills, or null if none.</summary>
    IssueGiverOccupation? Occupation { get; }

    /// <summary>Runtime culture StringId of the hero (for the issue's culture filter).</summary>
    string CultureStringId { get; }

    /// <summary>Relation between this hero and the main hero (issue offers gate on a minimum).</summary>
    int RelationWithPlayer { get; }
}
