namespace TAOM.Features.BanditManagement;

/// <summary>
/// Computes bandit-system scaling multipliers from PlayerProgress (0.0 → 1.0).
///
/// Curve shape: <c>multiplier = 1 + curve * playerProgress</c>, where <c>curve</c>
/// comes from MCM (<see cref="IBanditScalingSettingsProvider"/>) and defaults to 1.5.
/// At PlayerProgress = 0.0 (new campaign) the multiplier is 1.0× — vanilla behavior.
/// At PlayerProgress = 1.0 (endgame) the multiplier is 1 + curve = 2.5× by default.
///
/// All inputs are NaN/Infinity-guarded; multipliers cannot go below 1.0 so vanilla is
/// always the floor — bandits never get weaker than vanilla through this service.
/// </summary>
public interface IBanditScalingService
{
    /// <summary>Returns the multiplier for hideout count + parties-per-hideout density. Always >= 1.0.</summary>
    float GetDensityMultiplier(float playerProgress);

    /// <summary>Returns the multiplier for bandit party troop count on map. Always >= 1.0.</summary>
    float GetPartySizeMultiplier(float playerProgress);

    /// <summary>Returns the multiplier for first-fight + boss-fight troop counts at hideouts. Always >= 1.0.</summary>
    float GetBossFightMultiplier(float playerProgress);

    /// <summary>True when bandit scaling is enabled in MCM. When false, callers should fall through to vanilla.</summary>
    bool IsEnabled { get; }

    /// <summary>MCM-clamped cap on hideout count per bandit faction (vanilla = 9).</summary>
    int MaxHideoutsPerFactionCap { get; }

    /// <summary>MCM-clamped cap on bandit parties per hideout (vanilla = 3).</summary>
    int MaxPartiesPerHideoutCap { get; }

    /// <summary>Minimum bandit parties at a hideout for it to count as "infested" (vanilla = 2).
    /// Clamped to <c>[1, MaxPartiesPerHideoutCap]</c>. Lower values make hideouts visible sooner.</summary>
    int MinPartiesToInfest { get; }

    /// <summary>Hideouts each bandit faction starts with on a new campaign (vanilla = 7).
    /// MCM-clamped to <c>[1, 30]</c>. The early-game density lever.</summary>
    int InitialHideoutsPerFaction { get; }
}
