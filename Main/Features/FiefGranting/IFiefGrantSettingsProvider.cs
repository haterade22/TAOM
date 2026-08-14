namespace TAOM.Features.FiefGranting;

/// <summary>
/// Player-editable fief-grant tuning, backed by MCM (#458). MCM is the single config surface for
/// these weights on purpose: a parallel ModuleData copy would be a second place to validate, and
/// `csharp-architecture.md` records CombatMechanics drifting exactly that way when a JSON invariant
/// and an MCM clamp were written by different hands.
/// </summary>
public interface IFiefGrantSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>Merit multiplier for the clan that took the settlement. 1.0 disables the bonus.</summary>
    float CapturerBonus { get; }

    /// <summary>Merit multiplier for a clan holding no fortification at all.</summary>
    float LandlessBonus { get; }

    /// <summary>
    /// Per-fortification damping, applied as <c>1 / (1 + owned * penalty)</c>. Vanilla already divides
    /// by owned VALUE; this adds count-based damping so a clan holding many cheap castles is damped too.
    /// 0.0 disables it.
    /// </summary>
    float ConcentrationPenalty { get; }

    /// <summary>Merit multiplier when the clan's culture matches the settlement's.</summary>
    float CultureMatchBonus { get; }

    /// <summary>Merit multiplier when it does not. 1.0 disables the penalty.</summary>
    float CultureMismatchPenalty { get; }

    /// <summary>Merit multiplier for the ruling clan, which vanilla already hands +60 merit.</summary>
    float RulingClanFactor { get; }

    /// <summary>
    /// Share of the kingdom's fortifications above which the ruling clan loses its King's Vote
    /// override. 1.0 restores vanilla (the king can always override).
    /// </summary>
    float KingsVoteFiefShareCap { get; }

    /// <summary>
    /// When false the player's clan keeps every bonus term but is exempt from the concentration,
    /// ruling-clan, and culture-mismatch penalties.
    /// </summary>
    bool ApplyPenaltiesToPlayerClan { get; }
}
