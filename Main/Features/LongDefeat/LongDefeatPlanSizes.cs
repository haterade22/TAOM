using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>The deployment plan sizes a run boots with. The director and a co-op follower (#1280) boot the same frames from the same plan.</summary>
internal static class LongDefeatPlanSizes
{
    public static LdPlanSizes Of(RunPlan plan)
    {
        // The foot formation reserves a slot for the lord, unless he rides: a mounted lord joins the cavalry formation instead.
        var lordRides = plan.Loadout?.Mounted == true;
        var foot = plan.Garrison.Where(r => r.Slot == FormationSlot.Infantry).Sum(r => plan.Options.ScaledCount(r.Count)) + (lordRides ? 0 : 1);
        var ranged = plan.Garrison.Where(r => r.Slot == FormationSlot.Ranged).Sum(r => plan.Options.ScaledCount(r.Count));
        var attackerRanged = plan.Rules.LiveCap / 4;
        return new LdPlanSizes(foot, ranged, plan.Rules.LiveCap - attackerRanged, attackerRanged);
    }
}
