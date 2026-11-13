namespace TAOM.Features.Arachnophobia;

/// <summary>Arachnophobia Mode (docs/features/arachnophobia-mode.md). Tests fake it in each route it closes.</summary>
public interface IArachnophobiaService
{
    /// <summary>True when the mode took this troop out of play in the current game.</summary>
    bool IsHidden(string? troopId);

    /// <summary>Runs once per game, after the Monster size pass. Rebuilds the hidden set from that game's data.</summary>
    void ApplyForGame(bool enabled, bool isCampaign);
}
