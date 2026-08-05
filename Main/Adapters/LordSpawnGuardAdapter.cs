using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ILordSpawnGuardAdapter"/>. Resolves heroes through
/// <c>CampaignObjectManager.Find</c> and settlements through <c>Settlement.Find</c> (same as
/// <see cref="CultureConversionAdapter"/>). Computed TaleWorlds properties are reached with
/// <c>?.</c> per the adapter rules — several of them throw rather than return null before the
/// campaign is fully built.
/// </summary>
public class LordSpawnGuardAdapter : ILordSpawnGuardAdapter
{
    // Kingdom.InitialHomeSettlement has a private setter (Kingdom.cs:121, v1.4.7) while
    // Clan exposes the public SetInitialHomeSettlement. Cached once — never reflect per call.
    private static readonly MethodInfo KingdomHomeSetter =
        AccessTools.PropertySetter(typeof(Kingdom), nameof(Kingdom.InitialHomeSettlement));

    private readonly IModLogger _logger;

    public LordSpawnGuardAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string GetHeroMapFactionId(string heroId) => FindHero(heroId)?.MapFaction?.StringId;

    public string GetHeroCultureId(string heroId) => FindHero(heroId)?.Culture?.StringId;

    public bool FactionHasInitialHomeSettlement(string heroId)
        => FindHero(heroId)?.MapFaction?.InitialHomeSettlement != null;

    public bool AnySettlementHasHeroCulture(string heroId)
    {
        var culture = FindHero(heroId)?.Culture;
        if (culture == null)
            return true; // Unknown culture — vanilla's own lookup is not ours to second-guess.

        var all = Settlement.All;
        if (all == null)
            return true;

        for (var i = 0; i < all.Count; i++)
        {
            if (all[i]?.Culture == culture)
                return true;
        }
        return false;
    }

    public string GetHeroHomeSettlementId(string heroId) => FindHero(heroId)?.HomeSettlement?.StringId;

    public string GetHeroBornSettlementId(string heroId) => FindHero(heroId)?.BornSettlement?.StringId;

    public string GetClanLeaderSettlementId(string heroId)
    {
        var leader = FindHero(heroId)?.Clan?.Leader;
        return leader?.CurrentSettlement?.StringId ?? leader?.HomeSettlement?.StringId;
    }

    public string GetNearestFriendlySettlementId(string heroId) => FindNearest(heroId, friendlyOnly: true);

    public string GetNearestSettlementId(string heroId) => FindNearest(heroId, friendlyOnly: false);

    public bool SetFactionInitialHomeSettlement(string heroId, string settlementId)
    {
        var faction = FindHero(heroId)?.MapFaction;
        var settlement = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
        if (faction == null || settlement == null)
            return false;

        // Clan: public API, and it recomputes HomeSettlement from owned fiefs as a side effect.
        if (faction is Clan clan)
        {
            clan.SetInitialHomeSettlement(settlement);
            return true;
        }

        if (faction is Kingdom kingdom && KingdomHomeSetter != null)
        {
            KingdomHomeSetter.Invoke(kingdom, new object[] { settlement });
            return true;
        }

        _logger.LogWarning(
            $"LordSpawnGuardAdapter: no writable home-settlement setter for faction type " +
            $"'{faction.GetType().Name}' ('{faction.StringId}')");
        return false;
    }

    private string FindNearest(string heroId, bool friendlyOnly)
    {
        var hero = FindHero(heroId);
        var faction = hero?.MapFaction;
        if (faction == null)
            return null;

        var origin = hero.GetCampaignPosition();
        var all = Settlement.All;
        if (all == null)
            return null;

        Settlement best = null;
        var bestDistance = float.MaxValue;
        var haveOrigin = origin.IsValid();

        for (var i = 0; i < all.Count; i++)
        {
            var settlement = all[i];
            if (settlement == null)
                continue;
            if (friendlyOnly && FactionManager.IsAtWarAgainstFaction(settlement.MapFaction, faction))
                continue;

            // Without a usable origin (a hero in limbo — no settlement, no party) any settlement
            // beats none; take the first match rather than returning nothing.
            if (!haveOrigin)
                return settlement.StringId;

            var distance = origin.DistanceSquared(settlement.GatePosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = settlement;
            }
        }
        return best?.StringId;
    }

    private static Hero FindHero(string heroId)
        => string.IsNullOrEmpty(heroId)
            ? null
            : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
}
