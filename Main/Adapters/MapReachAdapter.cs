using System.Collections.Generic;
using TAOM.Core.Validation;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Memoisation over <c>MapDistanceModel</c>. See <see cref="IMapReachAdapter"/> for why the
/// measurement is anchored on the nearest owned fortification.
///
/// <para><b>Why the cache.</b> This is reached from <c>GetTargetScoreForFaction</c>, which runs on
/// the order of 1e5 times per campaign day across all thinking lords, and each miss walks every
/// fortification the attacking faction owns (at most 27 on this map, for Gondor). The reach curve
/// is smooth, so a value that is slightly stale costs nothing.</para>
///
/// <para><b>Three invalidations, because one is not enough.</b> Campaign identity, because the
/// container is built once at module load and this singleton outlives any one campaign. Fief count,
/// because reach is anchored on CURRENT holdings and a conquest immediately changes which targets
/// are near. Campaign day, as the backstop that catches a same-day swap where one fief was gained
/// and another lost, leaving the count unchanged.</para>
/// </summary>
public class MapReachAdapter : IMapReachAdapter
{
    /// <summary>Per-faction cache. Keyed faction-first so a single faction's entries can be dropped in one removal when its holdings change.</summary>
    private sealed class FactionReach
    {
        internal Settlement[] Fiefs;
        internal int FiefCount;
        internal readonly Dictionary<string, float> Distances = new Dictionary<string, float>();
    }

    private readonly Dictionary<string, FactionReach> _byFaction = new Dictionary<string, FactionReach>();

    private int _cacheDay = int.MinValue;
    private object _cacheCampaign;

    public float GetNormalizedDistanceToNearestFortification(Settlement targetSettlement, IFaction attackerFaction)
    {
        var campaign = Campaign.Current;

        // Ordered BEFORE the null-campaign return on purpose. Campaign.OnDestroy sets
        // Campaign.Current to null, and this singleton is otherwise the last thing holding the
        // finalized campaign's Settlement graph. Returning early without clearing would keep that
        // graph rooted through the whole of the next campaign's load, which is the heaviest
        // allocation phase in the process.
        ExpireStaleCaches(campaign);

        if (targetSettlement == null || attackerFaction == null || campaign == null) return float.NaN;

        string targetId = targetSettlement.StringId;
        string factionId = attackerFaction.StringId;
        if (targetId == null || factionId == null) return float.NaN;

        var entry = ResolveFactionEntry(attackerFaction, factionId);

        if (entry.Distances.TryGetValue(targetId, out float cached))
            return cached;

        float value = Measure(campaign, targetSettlement, entry.Fiefs);
        entry.Distances[targetId] = value;
        return value;
    }

    private float Measure(Campaign campaign, Settlement targetSettlement, Settlement[] fiefs)
    {
        // `?.` throughout: TaleWorlds computed properties can throw before a plain null check runs
        // (see adapters.md), and this path executes on every AI hourly tick.
        var distanceModel = campaign.Models?.MapDistanceModel;
        if (distanceModel == null) return float.NaN;

        float gap = campaign.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);

        // A degenerate normaliser cannot be divided by. Report unmeasurable rather than inventing a
        // number, and let the caller decline to suppress.
        if (!FiniteFloatValidator.IsFinite(gap) || gap <= 0f) return float.NaN;
        if (fiefs.Length == 0) return float.NaN;

        float best = float.MaxValue;
        bool measured = false;

        for (int i = 0; i < fiefs.Length; i++)
        {
            var origin = fiefs[i];
            if (origin == null) continue;

            // Six-argument overload deliberately: the five-argument form silently discards its
            // navigationCapability parameter.
            float d = distanceModel.GetDistance(
                targetSettlement, origin, isFromPort: false, isTargetingPort: false,
                MobileParty.NavigationType.All, out float _);

            // Unreachable pairs come back as a huge finite sentinel rather than infinity; that
            // normalises to a huge finite ratio, which the service maps onto its floor.
            if (!FiniteFloatValidator.IsFinite(d)) continue;
            measured = true;
            if (d < best) best = d;
        }

        if (!measured) return float.NaN;
        if (best < 0f) best = 0f;

        return best / gap;
    }

    private FactionReach ResolveFactionEntry(IFaction attackerFaction, string factionId)
    {
        var fiefs = attackerFaction.Fiefs;
        int currentCount = fiefs?.Count ?? 0;

        if (_byFaction.TryGetValue(factionId, out var entry))
        {
            // Holdings changed since this entry was built, so every cached distance for this
            // faction is measured against the wrong anchor set. Conquest is exactly when a
            // previously-far target becomes near, so leaving this until midnight would suppress the
            // axis of advance at the moment it opens.
            if (entry.FiefCount == currentCount) return entry;
            entry.Distances.Clear();
        }
        else
        {
            entry = new FactionReach();
            _byFaction[factionId] = entry;
        }

        entry.Fiefs = BuildFiefArray(fiefs);
        entry.FiefCount = currentCount;
        return entry;
    }

    private static Settlement[] BuildFiefArray(MBReadOnlyList<Town> fiefs)
    {
        if (fiefs == null || fiefs.Count == 0) return new Settlement[0];

        var buffer = new List<Settlement>(fiefs.Count);
        for (int i = 0; i < fiefs.Count; i++)
        {
            var settlement = fiefs[i]?.Settlement;
            if (settlement != null) buffer.Add(settlement);
        }
        return buffer.ToArray();
    }

    private void ExpireStaleCaches(Campaign campaign)
    {
        if (!ReferenceEquals(campaign, _cacheCampaign))
        {
            _cacheCampaign = campaign;
            _cacheDay = int.MinValue;
            _byFaction.Clear();
            if (campaign == null) return;
        }

        if (campaign == null) return;

        // ToDays is monotonic across years; GetDayOfYear would wrap and let an entry survive a
        // full year on the same calendar day. This is the backstop for a same-count fief swap,
        // which the count check above cannot see.
        int today = (int)CampaignTime.Now.ToDays;
        if (today == _cacheDay) return;

        _cacheDay = today;
        _byFaction.Clear();
    }
}
