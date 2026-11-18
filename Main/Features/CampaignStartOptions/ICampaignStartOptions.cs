namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// The values the current campaign pinned at its start, read by the features that follow them. Each
/// member is null when the campaign pinned nothing (an older save, or a campaign started some other
/// way): the reader then falls back to MCM, then to its JSON, exactly as before this feature.
/// Values only, no engine objects, so the service can outlive a campaign.
/// </summary>
public interface ICampaignStartOptions
{
    /// <summary>True when the campaign carries the TAOM marker key.</summary>
    bool IsPinned { get; }

    bool? WarOfTheRing { get; }

    /// <summary>The War of the Ring phase 1 and phase 2 trigger days, already validated.</summary>
    (int Phase1Day, int Phase2Day)? WarPhaseDays { get; }

    bool? TroopWeight { get; }

    /// <summary>War can be won (<c>MomentumVictoryEnabled</c>).</summary>
    bool? MomentumVictory { get; }

    /// <summary>Eye of Sauron hunters (<c>EyeOfSauronEnabled</c>).</summary>
    bool? EyeOfSauron { get; }

    /// <summary>Large AI armies (<c>EnableAiPartyScaling</c>).</summary>
    bool? AiPartyScaling { get; }

    /// <summary>The pinned preset id (Standard, Strict or Relaxed); null when the campaign pinned none.</summary>
    string? AlignmentPreset { get; }

    /// <summary>The four alignment switches the pinned preset fixes; null for Standard or none (follow MCM).</summary>
    AlignmentRules? AlignmentRules { get; }
}

/// <summary>Starts the per-session state at <c>SubModule.OnGameStart</c>.</summary>
public interface ICampaignStartSession
{
    /// <summary>
    /// Forgets the previous session's snapshot, pins a brand-new campaign's values (never a loaded
    /// one), and takes the snapshot the readers use. Must run before any reader.
    /// </summary>
    void Begin();
}

/// <summary>
/// What a new campaign gets for a setting the player left alone on the screen: MCM, else the JSON.
/// Never reads the campaign, so the main-menu screen cannot be affected by a previous session.
/// </summary>
public interface ICampaignStartDefaults
{
    StartDefaults Current();
}
