using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>
/// Buying and pressing claims. The player pays a bought claim in their people's special resource and a usurpation in
/// influence. AI clan leaders do the same in gold and influence on the weekly tick; nothing here runs per tick or per frame.
/// </summary>
public sealed partial class RealmsCampaign
{
    private readonly ClaimWorld _claimWorld = new();

    private FiefNeighbors Neighbors => _claimWorld.Neighbors(Config.Claims.Neighbors);

    public ClaimQuote QuoteBuyClaim(Hero hero, string titleId) =>
        _service.QuoteClaim(ClaimWorld.BuyerOf(hero), titleId, _claimWorld, Neighbors, Config.Claims);

    public ClaimQuote QuoteUsurp(Hero hero, string titleId) =>
        _service.QuoteUsurp(hero.StringId, hero.Clan?.StringId, titleId, _claimWorld, Config.Claims);

    /// <summary>The player buys a claim, paying in their special resource. False when it is not allowed or not affordable.</summary>
    public bool PlayerBuyClaim(string titleId)
    {
        var hero = Hero.MainHero;
        if (hero == null || !MayWrite())
            return false;
        var quote = QuoteBuyClaim(hero, titleId);
        if (!quote.Allowed || !SpecialResources.TrySpend(hero, quote.Cost))
            return false;
        var events = _service.BuyClaim(ClaimWorld.BuyerOf(hero), titleId, CampaignTime.Now.ToDays, _claimWorld, Neighbors, Config.Claims);
        HolderResents(hero, events, Config.Claims.BuyRelation);
        Report(events);
        return events.Count > 0;
    }

    /// <summary>The player presses a claim, paying influence. False when it is not allowed or not affordable.</summary>
    public bool PlayerUsurp(string titleId)
    {
        var hero = Hero.MainHero;
        var clan = hero?.Clan;
        if (hero == null || clan == null || !MayWrite())
            return false;
        var quote = QuoteUsurp(hero, titleId);
        if (!quote.Allowed || clan.Influence < quote.Cost)
            return false;
        ChangeClanInfluenceAction.Apply(clan, -quote.Cost);
        var events = _service.Usurp(hero.StringId, clan.StringId, titleId, _claimWorld, Config.Claims);
        HolderResents(hero, events.Take(1).ToList(), Config.Claims.UsurpRelation);
        Report(events);
        return events.Count > 0;
    }

    /// <summary>
    /// AI clan leaders restore or form the vacant titles they can (influence), press the claims they can afford
    /// (influence) and, with <c>ai_chance</c>, buy one claim on a bordering title (gold). Weekly and only over clans that
    /// own a town or castle: a few hundred cheap lookups.
    /// </summary>
    private void WeeklyAiClaims()
    {
        var config = Config.Claims;
        if (!config.AiEnabled)
            return;

        var events = new List<TitleEvent>();
        foreach (var clan in Clan.All.ToList())
        {
            var leader = clan?.Leader;
            if (clan == null || clan.IsEliminated || clan.IsBanditFaction || clan == Clan.PlayerClan
                || leader == null || !leader.IsAlive || leader == Hero.MainHero || !clan.Settlements.Any(s => s.IsTown || s.IsCastle))
                continue;

            foreach (var (titleId, cost, restore) in _service.FormOffers(leader.StringId, _claimWorld, config))
            {
                if (clan.Influence < cost * config.AiReserve)
                    break;
                // Quoted again inside: an earlier formation this week may have changed the picture. Paid only when it happens.
                var formed = restore
                    ? _service.Restore(leader.StringId, titleId, _claimWorld, config)
                    : _service.Form(leader.StringId, titleId, _claimWorld, config);
                if (formed.Count > 0)
                    ChangeClanInfluenceAction.Apply(clan, -cost);
                events.AddRange(formed);
            }

            foreach (var (titleId, cost) in _service.UsurpOffers(leader.StringId, clan.StringId, _claimWorld, config))
            {
                if (clan.Influence < cost * config.AiReserve)
                    break;
                ChangeClanInfluenceAction.Apply(clan, -cost);
                var usurped = _service.Usurp(leader.StringId, clan.StringId, titleId, _claimWorld, config);
                HolderResents(leader, usurped.Take(1).ToList(), config.UsurpRelation);
                events.AddRange(usurped);
            }

            if (MBRandom.RandomFloat >= config.AiChance)
                continue;
            var buyer = ClaimWorld.BuyerOf(leader);
            var affordable = _service.ClaimOffers(buyer, _claimWorld, Neighbors, config)
                .Where(o => leader.Gold >= o.Price * config.AiGoldPerPoint * config.AiReserve).ToList();
            if (affordable.Count == 0)
                continue;
            var (title, price) = affordable[MBRandom.RandomInt(affordable.Count)];
            GiveGoldAction.ApplyBetweenCharacters(leader, null, price * config.AiGoldPerPoint, true);
            var bought = _service.BuyClaim(buyer, title.Id, CampaignTime.Now.ToDays, _claimWorld, Neighbors, config);
            HolderResents(leader, bought, config.BuyRelation);
            events.AddRange(bought);
        }

        Report(events);
    }

    /// <summary>The holder (the events' previous holder) thinks less of the hero who claimed or took their title.</summary>
    private static void HolderResents(Hero hero, IEnumerable<TitleEvent> events, int relation)
    {
        if (relation == 0)
            return;
        foreach (var holderId in events.Select(e => e.PreviousHolderId).Where(id => id != null && id != hero.StringId).Distinct())
        {
            var holder = Hero.Find(holderId);
            if (holder == null || !holder.IsAlive)
                continue;
            if (hero == Hero.MainHero)
                ChangeRelationAction.ApplyPlayerRelation(holder, relation);
            else
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, holder, relation, false);
        }
    }
}
