namespace TAOM.Features.FiefGranting;

/// <summary>
/// Player-editable fief-grant tuning, backed by MCM (#458, #565). MCM is the single config surface
/// for these weights on purpose: a parallel ModuleData copy would be a second place to validate, and
/// `csharp-architecture.md` records CombatMechanics drifting exactly that way when a JSON invariant
/// and an MCM clamp were written by different hands.
/// </summary>
public interface IFiefGrantSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>
    /// Merit multiplier for the clan that carried the winning assault (the top contribution share).
    /// Every other clan that fielded a party gets <c>1 + (bonus - 1) * share</c>. 1.0 disables it.
    /// </summary>
    float CapturerBonus { get; }

    /// <summary>
    /// Merit multiplier for a clan that had no party in the winning assault. Applied only when the
    /// settlement has a participation record, and to the player's clan regardless of the exemption.
    /// 1.0 disables it.
    /// </summary>
    float AbsentFromSiegeFactor { get; }

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
    /// ruling-clan, and culture-mismatch penalties. Since #565 this is true by default: the MCM knob
    /// is the inverted "Exempt Your Clan From Penalties", off unless the player turns it on. The
    /// absent-from-siege factor is not a holdings penalty and applies either way.
    /// </summary>
    bool ApplyPenaltiesToPlayerClan { get; }
}
