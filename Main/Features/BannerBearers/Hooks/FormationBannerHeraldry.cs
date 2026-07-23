using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerBearers.Hooks;

// Boundary reader for THE SIEGE GUARD (see BannerBearerAssignmentMissionLogic). Pulls the renderable
// banner-data count off each of a formation's bearer-candidate troops so IBannerBearerService can
// decide whether SetFormationBanner is safe. Kept out of the MissionLogic to hold that entry point
// under the ADR-002 150-line ceiling; all it does is read sealed engine types and emit ints.
internal static class FormationBannerHeraldry
{
    // Fill `output` with one entry per bearer-candidate: the count of renderable BannerData on that
    // troop's heraldry. The engine picks the bearer by priority from ALL non-detached units (see
    // BannerBearerLogic.FindBannerBearableAgents), so every candidate is sampled — checking only slot 0
    // would let a null-banner troop in a mixed-origin formation slip through and access-violate.
    public static void CollectCandidateBannerDataCounts(Formation formation, List<int> output)
    {
        output.Clear();

        foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
        {
            if (unit is Agent agent)
                output.Add(SafeBannerDataCount(agent));
        }

        // Every unit detached (UnitsWithoutLooseDetachedOnes empty while CountOfUnits > 0).
        if (output.Count == 0)
        {
            var first = formation.GetFirstUnit();
            if (first != null)
                output.Add(SafeBannerDataCount(first));
        }
    }

    // The heraldry the native tableau renders is seeded at spawn from agent.Origin.Banner. Returns 0
    // (→ the guard treats it as unrenderable and skips) when the origin has no Banner or an empty
    // BannerDataList.
    //
    // The try/catch is load-bearing, not defensive noise: PartyAgentOrigin.Banner is a COMPUTED getter
    // that falls through to Party.MapFaction.Banner / HeroObject.MapFaction.Banner with no null guard,
    // so it can THROW (not merely return null) for a factionless party/hero — the adapters.md "computed
    // getter throws before your null check" trap that `?.` cannot cover. A guard whose whole job is to
    // prevent a crash must never itself throw, so any failure degrades to 0 (skip).
    private static int SafeBannerDataCount(Agent agent)
    {
        try
        {
            return agent.Origin?.Banner?.GetBannerDataListCount() ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
