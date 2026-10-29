namespace TAOM.Features.StagedSiege.Domain;

/// <summary>A read-only view of one team's staged tactic, for the status line and the log.</summary>
public interface IStagedTacticProbe
{
    StagedSide Side { get; }

    /// <summary>The team's AI picked this tactic as its current one.</summary>
    bool IsCurrent { get; }

    string Status { get; }
}
