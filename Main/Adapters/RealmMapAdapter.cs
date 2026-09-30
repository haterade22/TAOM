using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Validation;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IRealmMapAdapter"/> over the live campaign (v1.5.3). A fief's realm is its map faction
/// (<c>Town.MapFaction</c> is the owner clan's, which is the clan's kingdom or the clan itself), so a
/// rebellion, which creates a clan rather than a kingdom, is a realm of its own. The "clan:" key format
/// never leaves this class. The relation to the player follows <c>SettlementNameplateVM.RefreshRelationStatus</c>
/// in the same order (at war, same faction, allied, else neutral), so the war map and the nameplates agree.
/// </summary>
public sealed class RealmMapAdapter : IRealmMapAdapter
{
    private const string ClanPrefix = "clan:";

    public IReadOnlyList<FiefSite> GetFiefSites()
    {
        var all = Settlement.All;
        if (all == null)
            return new List<FiefSite>();
        var sites = new List<FiefSite>();
        foreach (var settlement in all)
        {
            if (settlement == null || !(settlement.IsTown || settlement.IsCastle))
                continue;
            var villages = new List<MapPoint>();
            if (settlement.BoundVillages != null)
            {
                foreach (var village in settlement.BoundVillages)
                {
                    var bound = village?.Settlement;
                    if (bound != null)
                        villages.Add(Point(bound.Position));
                }
            }
            sites.Add(new FiefSite(settlement.StringId, Point(settlement.Position), settlement.IsTown, villages));
        }
        return sites;
    }

    public string? RealmOf(string fiefId) => KeyOf(Settlement.Find(fiefId)?.MapFaction);

    public string? PlayerRealm => KeyOf(Hero.MainHero?.MapFaction);

    public RealmRelation RelationToPlayer(string realm)
    {
        var faction = FactionOf(realm);
        var player = Hero.MainHero?.MapFaction;
        if (faction == null || player == null)
            return RealmRelation.Neutral;
        if (FactionManager.IsAtWarAgainstFaction(faction, player))
            return RealmRelation.Enemy;
        if (DiplomacyHelper.IsSameFactionAndNotEliminated(faction, player))
            return RealmRelation.Own;
        return DiplomacyHelper.HasAllianceWithFaction(faction, player) ? RealmRelation.Ally : RealmRelation.Neutral;
    }

    public string? CultureOfRealm(string realm) => FactionOf(realm)?.Culture?.StringId;

    public string RealmName(string realm) => FactionOf(realm)?.Name?.ToString() ?? realm;

    public bool TryGetMainPartyPosition(out float x, out float y)
    {
        x = y = 0f;
        var party = MobileParty.MainParty;
        if (party == null)
            return false;
        var p = party.Position.ToVec2();
        if (!FiniteFloatValidator.IsFinite(p.x) || !FiniteFloatValidator.IsFinite(p.y))
            return false;
        (x, y) = (p.x, p.y);
        return true;
    }

    public double CampaignHours => CampaignTime.Now.ToHours;

    private static string? KeyOf(IFaction? faction) => faction switch
    {
        Kingdom kingdom => kingdom.StringId,
        Clan clan => ClanPrefix + clan.StringId,
        _ => null,
    };

    private static IFaction? FactionOf(string realm)
    {
        if (string.IsNullOrEmpty(realm))
            return null;
        if (realm.StartsWith(ClanPrefix, StringComparison.Ordinal))
        {
            string id = realm.Substring(ClanPrefix.Length);
            foreach (var clan in Clan.All)
            {
                if (clan != null && clan.StringId == id)
                    return clan;
            }
            return null;
        }
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom != null && kingdom.StringId == realm)
                return kingdom;
        }
        return null;
    }

    private static MapPoint Point(CampaignVec2 position)
    {
        var p = position.ToVec2();
        return new MapPoint(p.x, p.y);
    }
}
