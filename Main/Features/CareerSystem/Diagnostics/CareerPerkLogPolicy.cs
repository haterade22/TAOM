using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// Which campaign passives get a <c>[CareerPerks]</c> line when they apply (#613). Only the ones
/// that fire on an event: a battle end, an upgrade, a daily tick. The per-tick ones (party speed
/// every frame on the map, morale, wages, spotting range) leave their "Career" line on the
/// ExplainedNumber and are read by <c>taom.career_perks</c>.
/// </summary>
public static class CareerPerkLogPolicy
{
    public static bool IsEventScoped(PassiveEffectType type)
        => type == PassiveEffectType.RenownGain
        || type == PassiveEffectType.TroopUpgradeCost
        || type == PassiveEffectType.HeroHealing
        || type == PassiveEffectType.SmithingCostReduction;
}
