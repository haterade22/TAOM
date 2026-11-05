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
}
