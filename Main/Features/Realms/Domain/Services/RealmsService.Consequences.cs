using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// What titles do to the people who hold them, read on the weekly tick: fiefs held without their title, the pull of a
/// liege over vassals, and the tribute vassals owe. These only report; the game applies relation, loyalty and gold.
/// </summary>
public sealed partial class RealmsService
{
    /// <summary>
    /// Towns and castles whose owner is not the holder of their title while that holder lives in another clan: the holder
    /// by right resents the lord who sits in their hall.
    /// </summary>
    public IReadOnlyList<TitleGap> TitleGaps(IClaimWorld world)
    {
        var gaps = new List<TitleGap>();
        foreach (var title in Catalog.All)
        {
            if ((title.Rank != TitleRank.County && title.Rank != TitleRank.Barony) || title.SettlementId == null)
                continue;
            var holder = Ledger.HolderOf(title.Id);
            if (holder == null || Ledger.IsPending(title.Id))
                continue;
            var owner = world.OwnerOf(title.SettlementId);
            if (owner == null || owner == holder)
                continue;
            var ownerClan = world.ClanOf(owner);
            if (ownerClan != null && ownerClan == world.ClanOf(holder))
                continue;
            gaps.Add(new TitleGap(title.Id, title.SettlementId, holder, owner));
        }

        return gaps;
    }

    /// <summary>
    /// Each liege and vassal pair, once: the vassal holds a title directly below one of <see cref="FeudalConfig.LiegeMinRank"/>
    /// or higher held by the liege, and the two live in different clans.
    /// </summary>
    public IReadOnlyList<(string LiegeId, string VassalId)> LiegeBonds(IClaimWorld world, FeudalConfig config)
    {
        var bonds = new HashSet<(string, string)>();
        foreach (var title in Catalog.All)
        {
            if (title.ParentId == null)
                continue;
            var liegeTitle = Catalog.Get(title.ParentId);
            if (liegeTitle == null || liegeTitle.Rank < config.LiegeMinRank)
                continue;
            var vassal = Ledger.HolderOf(title.Id);
            var liege = Ledger.HolderOf(liegeTitle.Id);
            if (vassal == null || liege == null || vassal == liege || SameClan(vassal, liege, world))
                continue;
            bonds.Add((liege, vassal));
        }

        return bonds.OrderBy(b => b.Item1, StringComparer.Ordinal).ThenBy(b => b.Item2, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The weekly tribute each vassal owes each liege: for every title with a <see cref="FeudalConfig.TributeOf"/> its
    /// holder pays the holder of its direct liege title, when both serve the same realm in different clans. Summed per pair.
    /// </summary>
    public IReadOnlyList<Tribute> Tributes(IClaimWorld world, FeudalConfig config)
    {
        var owed = new Dictionary<(string, string), int>();
        foreach (var title in Catalog.All)
        {
            var gold = config.TributeOf(title.Rank);
            if (gold <= 0 || title.ParentId == null)
                continue;
            var payer = Ledger.HolderOf(title.Id);
            var payee = Ledger.HolderOf(title.ParentId);
            if (payer == null || payee == null || payer == payee || SameClan(payer, payee, world))
                continue;
            var realm = world.KingdomOfHero(payer);
            if (realm == null || world.KingdomOfHero(payee) != realm)
                continue;
            owed.TryGetValue((payer, payee), out var sum);
            owed[(payer, payee)] = sum + gold;
        }

        return owed.OrderBy(o => o.Key.Item1, StringComparer.Ordinal).ThenBy(o => o.Key.Item2, StringComparer.Ordinal)
            .Select(o => new Tribute(o.Key.Item1, o.Key.Item2, o.Value)).ToList();
    }

    private static bool SameClan(string a, string b, IClaimWorld world)
    {
        var clan = world.ClanOf(a);
        return clan != null && clan == world.ClanOf(b);
    }
}
