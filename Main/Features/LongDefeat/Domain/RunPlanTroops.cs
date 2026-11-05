using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>Every troop a run can spawn, so the launcher can refuse a missing one before the run instead of a batch spawning none after the domain has dequeued it.</summary>
internal static class RunPlanTroops
{
    /// <summary>The garrison, the shop, every authored wave and every tier target, each id once.</summary>
    public static IReadOnlyList<string> AllTroopIds(RunPlan plan) =>
        plan.Garrison.Select(r => r.TroopId)
            .Concat(plan.Shop.Offers.Select(o => o.TroopId))
            .Concat(plan.Waves.Authored.SelectMany(w => w.Mix.Select(m => m.TroopId)))
            .Concat(plan.Waves.Endless.Targets)
            .Distinct()
            .ToList();

    /// <summary>The troops of the player's side: the garrison and the shop, each id once. Their groups are read from the engine's characters before the run.</summary>
    public static IReadOnlyList<string> DefenderTroopIds(RunPlan plan) =>
        plan.Garrison.Select(r => r.TroopId).Concat(plan.Shop.Offers.Select(o => o.TroopId)).Distinct().ToList();

    /// <summary>
    /// The troop categories the garrison or the shop holds a troop of: static data, so a replay sees the same pool the run drew from. A boon row of a category outside it is never
    /// eligible. A rider on the shop list is cavalry whatever slot it fills.
    /// </summary>
    public static IReadOnlyCollection<BoonCategory> DefenderGroups(RunPlan plan) =>
        plan.Garrison.Select(r => plan.GroupOf(r.TroopId, r.Slot))
            .Concat(plan.Shop.Offers.Select(o => plan.GroupOf(o.TroopId, o.Slot)))
            .Select(BoonTargets.CategoryOf)
            .OfType<BoonCategory>()
            .Distinct()
            .ToList();
}
