using System.Collections.Generic;

namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// POCO deserialized from <c>field_commission/field_commission_config.json</c>. Mirrors
/// <c>CultureConversionConfig</c>'s shape (plain settable properties, semantic validation lives in
/// the provider, not here).
/// </summary>
public class FieldCommissionConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Player-party-healthy-count / enemy-side-healthy-count must be below this for merit
    /// to accrue in that battle. Lower = the fight must be more lopsided AGAINST the player.</summary>
    public float RatioThreshold { get; set; } = 1.3f;

    /// <summary>Merit points banked per kill of a given troop type in an eligible, won battle.</summary>
    public int MeritPerKill { get; set; } = 1;

    /// <summary>Merit required before a promotion offer is queued for that troop type. Raised 8 → 32
    /// on 2026-08-08: merit pools per troop TYPE rather than per soldier, so a 30-strong stack shares
    /// one counter and cleared the old bar inside a single battle at well under a kill each.</summary>
    public int MeritThreshold { get; set; } = 32;

    /// <summary>Extra companions allowed beyond the clan-tier limit before offers defer for lack
    /// of room. Default 0 — the tier limit is strict.</summary>
    public int RetainerAllowance { get; set; } = 0;

    /// <summary>Hard ceiling on how many promotion offers ONE won battle may queue, across all troop
    /// types. Must be >= 1. A cap is needed at all because without one the count is <c>sum over troop
    /// types of min(count, merit/threshold)</c>, and each offer is a separate game-pausing modal the
    /// player cannot dismiss in bulk. Default 2 (was 1, the donor's behaviour): at a threshold of 32
    /// a battle that earns two promotions is a genuine result rather than routine, and holding it to
    /// one would drip-feed merit the player has already earned. Unspent merit is never lost — it
    /// re-queues after the next won battle.</summary>
    public int MaxOffersPerBattle { get; set; } = 2;

    /// <summary>Skill points of "budget" granted per hero level when building a promoted
    /// companion's skills from its troop template (see <c>CommissionSkillBudget</c>).</summary>
    public int SkillPointsPerLevel { get; set; } = 5;

    /// <summary>Writes an <c>[FieldCommission]</c> trace of eligibility, merit accrual, offer
    /// queueing and each completed promotion to the TAOM debug log. Off by default — it is the
    /// switch to turn on when a player reports a promotion behaving oddly, so their next log
    /// answers the question. Faults are logged regardless of this setting.</summary>
    public bool Diagnostics { get; set; } = false;

    /// <summary>Race ids (by name, matched via <c>IRaceManager</c>) allowed to be promoted.
    /// Unknown/invalid names fail closed — see <c>FieldCommissionMeritService</c>'s promotability
    /// gate and the "validate before lookup" rule.</summary>
    public List<string> AllowedRaceNames { get; set; } = new List<string> { "human", "dwarf", "elf" };
}
