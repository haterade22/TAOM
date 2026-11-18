namespace TAOM.Features.AiPartySize;

/// <summary>
/// Whether Large AI armies (<c>EnableAiPartyScaling</c>) is on: the campaign's pinned choice, else MCM, else on.
/// Every reader asks this, never MCM directly (a gate test fails a new direct read).
/// </summary>
public interface IAiPartyScalingSettingsProvider
{
    bool Enabled { get; }

    /// <summary>What a new campaign gets when the player leaves the option alone: MCM, else on.</summary>
    bool NewCampaignDefault { get; }
}
