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

    /// <summary>Merit required before a promotion offer is queued for that troop type.</summary>
    public int MeritThreshold { get; set; } = 8;

    /// <summary>Extra companions allowed beyond the clan-tier limit before offers defer for lack
    /// of room. Default 0 — the tier limit is strict.</summary>
    public int RetainerAllowance { get; set; } = 0;

    /// <summary>Skill points of "budget" granted per hero level when building a promoted
    /// companion's skills from its troop template (see <c>CommissionSkillBudget</c>).</summary>
    public int SkillPointsPerLevel { get; set; } = 5;

    /// <summary>Race ids (by name, matched via <c>IRaceManager</c>) allowed to be promoted.
    /// Unknown/invalid names fail closed — see <c>FieldCommissionMeritService</c>'s promotability
    /// gate and the "validate before lookup" rule.</summary>
    public List<string> AllowedRaceNames { get; set; } = new List<string> { "human", "dwarf", "elf" };
}
