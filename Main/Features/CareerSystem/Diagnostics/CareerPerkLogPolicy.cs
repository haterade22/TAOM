using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// Which campaign passives get a <c>[CareerPerks]</c> line when they apply (#613): a battle end,
/// troop upgrade costs, party healing, smithing. Not every one is rare: HeroHealing runs twice per
/// in-game hour for the main party and TroopUpgradeCost once per upgrade target as party-screen rows
/// build, so <c>CareerPassiveService</c> writes each distinct set of numbers once between cache
/// refreshes. The per-tick ones (party speed every frame on the map, morale, wages, spotting range)
/// leave their "Career" line on the ExplainedNumber and are read by <c>taom.career_perks</c>.
/// </summary>
public static class CareerPerkLogPolicy
{
    public static bool IsEventScoped(PassiveEffectType type)
        => type == PassiveEffectType.RenownGain
        || type == PassiveEffectType.TroopUpgradeCost
        || type == PassiveEffectType.HeroHealing
        || type == PassiveEffectType.SmithingCostReduction;
}
