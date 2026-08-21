using System.Collections.Generic;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Day-scoped memoisation over <c>MapDistanceModel</c>. See <see cref="IMapReachAdapter"/> for why
/// the measurement is anchored on the nearest owned fortification.
///
/// <para><b>Why the cache.</b> This is reached from <c>GetTargetScoreForFaction</c>, which the
/// feature doc measures at 500 to 2000 calls per AI cycle, and each miss walks every fortification
/// the attacking faction owns (93 for Gondor). Ownership changes at most a handful of times a day,
/// and the reach curve is smooth, so a value that is one day stale costs nothing. The cache clears
/// itself when the campaign day changes rather than needing a behaviour to tick it.</para>
/// </summary>
public class MapReachAdapter : IMapReachAdapter
{
    // settlementId -> factionId -> normalised distance. Two levels rather than a composite string
    // key, because concatenating a key would allocate on every call in the hot path.
    private readonly Dictionary<string, Dictionary<string, float>> _cache =
        new Dictionary<string, Dictionary<string, float>>();

    private readonly Dictionary<string, Settlement[]> _fiefCache = new Dictionary<string, Settlement[]>();

    private int _cacheDay = int.MinValue;

    // Campaign identity, not just the day. The container is built once in OnSubModuleLoad, so this
    // singleton outlives any one campaign: quitting to the menu and loading a DIFFERENT save that
    // happens to sit on the same day number would otherwise leave _fiefCache holding Settlement
    // objects from the finalized campaign, and hand them to a native MapDistanceModel call.
    private object _cacheCampaign;

    public float GetNormalizedDistanceToNearestFortification(Settlement targetSettlement, IFaction attackerFaction)
    {
        if (targetSettlement == null || attackerFaction == null) return float.NaN;

        var campaign = Campaign.Current;
        if (campaign == null) return float.NaN;

        string targetId = targetSettlement.StringId;
        string factionId = attackerFaction.StringId;
        if (targetId == null || factionId == null) return float.NaN;

        ExpireCacheIfDayChanged();

        if (_cache.TryGetValue(targetId, out var perFaction) && perFaction.TryGetValue(factionId, out float cached))
            return cached;

        float value = Measure(campaign, targetSettlement, attackerFaction, factionId);

        if (perFaction == null)
        {
            perFaction = new Dictionary<string, float>();
            _cache[targetId] = perFaction;
        }
        perFaction[factionId] = value;

        return value;
    }

    private float Measure(Campaign campaign, Settlement targetSettlement, IFaction attackerFaction, string factionId)
    {
        // `?.` throughout: TaleWorlds computed properties can throw before a plain null check runs
        // (see adapters.md), and this path executes on every AI hourly tick.
        var distanceModel = campaign.Models?.MapDistanceModel;
        if (distanceModel == null) return float.NaN;

        float gap = campaign.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);

        // A degenerate normaliser cannot be divided by. Report unmeasurable rather than inventing a
        // number, and let the caller decline to suppress.
        if (!FiniteFloatValidator.IsFinite(gap) || gap <= 0f) return float.NaN;

        var fiefs = ResolveFiefSettlements(attackerFaction, factionId);
        if (fiefs.Length == 0) return float.NaN;

        float best = float.MaxValue;
        for (int i = 0; i < fiefs.Length; i++)
        {
            var origin = fiefs[i];
            if (origin == null) continue;

            // Six-argument overload deliberately: the five-argument form silently discards its
            // navigationCapability parameter.
            float d = distanceModel.GetDistance(
                targetSettlement, origin, isFromPort: false, isTargetingPort: false,
                MobileParty.NavigationType.All, out float _);

            // Unreachable pairs come back as a huge sentinel rather than infinity; a finite huge
            // value normalises to a huge finite ratio, which the service maps onto its floor.
            if (!FiniteFloatValidator.IsFinite(d)) continue;
            if (d < best) best = d;
        }

        if (best >= float.MaxValue) return float.NaN;
        if (best < 0f) best = 0f;

        return best / gap;
    }

    private Settlement[] ResolveFiefSettlements(IFaction attackerFaction, string factionId)
    {
        if (_fiefCache.TryGetValue(factionId, out var cached)) return cached;

        var fiefs = attackerFaction.Fiefs;
        if (fiefs == null || fiefs.Count == 0)
        {
            _fiefCache[factionId] = new Settlement[0];
            return _fiefCache[factionId];
        }

        var buffer = new List<Settlement>(fiefs.Count);
        for (int i = 0; i < fiefs.Count; i++)
        {
            var settlement = fiefs[i]?.Settlement;
            if (settlement != null) buffer.Add(settlement);
        }

        var result = buffer.ToArray();
        _fiefCache[factionId] = result;
        return result;
    }

    private void ExpireCacheIfDayChanged()
    {
        // A campaign switch invalidates everything regardless of the date. Reference equality is
        // the check: Campaign.Current is a fresh object per campaign, and two campaigns can sit on
        // the same day number.
        if (!ReferenceEquals(Campaign.Current, _cacheCampaign))
        {
            _cacheCampaign = Campaign.Current;
            _cacheDay = int.MinValue;
            _cache.Clear();
            _fiefCache.Clear();
        }

        // ToDays is monotonic across years; GetDayOfYear would wrap and let an entry survive a
        // full year on the same calendar day.
        int today = (int)CampaignTime.Now.ToDays;
        if (today == _cacheDay) return;

        _cacheDay = today;
        _cache.Clear();
        _fiefCache.Clear();
    }
}
