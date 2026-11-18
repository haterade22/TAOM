namespace TAOM.Features.TroopWeight;

/// <summary>
/// Whether Troop Weight is on: the campaign's pinned choice, else MCM, else on. Every reader asks this,
/// never MCM directly (a gate test fails a new direct read of <c>EnableTroopWeight</c>).
/// </summary>
public interface ITroopWeightSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>What a new campaign gets when the player leaves the option alone: MCM, else on.</summary>
    bool NewCampaignDefault { get; }
}
