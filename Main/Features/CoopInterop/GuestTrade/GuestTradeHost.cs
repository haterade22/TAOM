using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// The host's side of guest trade (#1329): checks a guest's request, then runs TAOM's own emissary or armoury for
/// that guest's hero, so the host alone changes the roster, purse and inventory that Coop then syncs. The relay
/// names the hero from the sender's connection, never from the message. Design credit: minimalcash63 (TAOM.CoopEmissaryArmory); request checks after Sternab
/// (TAOM-Coop).
/// </summary>
public sealed class GuestTradeHost
{
    private const int MaxTextLength = 128;
    private const int MaxQuantity = 1000;

    private readonly IGuestTradeHeroAdapter _heroes;
    private readonly IGuestTradeShops _shops;
    private readonly ISpecialResourceService _resources;
    private readonly IModLogger _logger;

    public GuestTradeHost(IGuestTradeHeroAdapter heroes, IGuestTradeShops shops, ISpecialResourceService resources, IModLogger logger)
    {
        _heroes = heroes;
        _shops = shops;
        _resources = resources;
        _logger = logger;
    }

    public GuestTradeReply Handle(string heroId, GuestTradeRequest request)
    {
        if (!IsWellFormed(request))
            return Refuse(request, GuestTradeRefusal.BadRequest);
        if (string.IsNullOrEmpty(heroId))
            return Refuse(request, GuestTradeRefusal.NotFreeLeader);

        GuestTradeReply reply;
        try
        {
            reply = Run(heroId, request);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GuestTrade] {request.Action} for {heroId} failed on the host: {ex.Message}");
            reply = Refuse(request, GuestTradeRefusal.HostFailed);
        }
        _logger.LogInfo($"[GuestTrade] {request.Action} for {heroId} at {request.SettlementId}: {reply.Refusal}, status {reply.Status}.");
        return reply;
    }

    private GuestTradeReply Run(string heroId, GuestTradeRequest request)
    {
        var hero = _heroes.Find(heroId);
        if (hero == null || !hero.IsFreeLeader)
            return Refuse(request, GuestTradeRefusal.NotFreeLeader);
        if (hero.InBattle || !string.Equals(hero.SettlementId, request.SettlementId, StringComparison.Ordinal))
            return Refuse(request, GuestTradeRefusal.NotInSettlement);

        return request.Action is GuestTradeAction.EmissaryBalance or GuestTradeAction.EmissaryBuy
            ? Emissary(hero, request)
            : Armoury(hero, request);
    }

    private GuestTradeReply Emissary(GuestTradeHero hero, GuestTradeRequest request)
    {
        var owner = hero.Owner;
        if (owner == null || hero.AtWarWithSettlement)
            return Refuse(request, GuestTradeRefusal.NoEmissaryTrade);
        var emissary = _shops.Emissary(hero);
        if (!emissary.IsEnabled || !emissary.IsKeySettlement(request.SettlementId)
            || !emissary.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId))
            return Refuse(request, GuestTradeRefusal.NoEmissaryTrade);

        var reply = new GuestTradeReply(request.RequestId, request.Action, GuestTradeRefusal.None);
        if (request.Action == GuestTradeAction.EmissaryBuy)
        {
            var result = emissary.Purchase(hero.HeroId, owner.OwnerKingdomId, owner.OwnerCultureId, request.Subject!, request.Quantity);
            reply = reply with
            {
                Status = (int)result.Status,
                Quantity = result.Quantity,
                TotalCost = result.TotalCost,
                ResourceName = result.ResourceDisplayName,
            };
        }
        return WithBalance(reply, hero.HeroId, owner.OwnerKingdomId, owner.OwnerCultureId);
    }

    private GuestTradeReply Armoury(GuestTradeHero hero, GuestTradeRequest request)
    {
        if (!_shops.HasArmoury(request.SettlementId))
            return Refuse(request, GuestTradeRefusal.NoArmoury);

        var level = _shops.ArmouryLevel(request.SettlementId);
        var reply = new GuestTradeReply(request.RequestId, request.Action, GuestTradeRefusal.None, ArmouryLevel: level);
        if (request.Action is GuestTradeAction.LadderHandIn or GuestTradeAction.LadderClaim)
        {
            reply = reply with { Status = (int)Ladder(hero, request, level) };
        }
        else if (request.Action == GuestTradeAction.ArmouryUpgrade)
        {
            var modifier = string.IsNullOrEmpty(request.Modifier) ? null : request.Modifier;
            var offer = _shops.ArmouryOffers(hero, level).FirstOrDefault(o =>
                string.Equals(o.SourceItemId, request.Subject, StringComparison.Ordinal)
                && string.Equals(o.ModifierId, modifier, StringComparison.Ordinal)
                && string.Equals(o.TargetItemId, request.Target, StringComparison.Ordinal));
            var outcome = offer == null ? UpgradeBlock.NoLongerCarried
                : offer.CanUpgrade ? _shops.Upgrade(hero, offer)
                : offer.Block;
            reply = reply with { Status = (int)outcome };
        }
        return WithBalance(WithLadder(reply, hero, level), hero.HeroId, hero.Player.KingdomId, hero.Player.CultureId);
    }

    // The switch is read first: with it off no ladder is built for the guest at all.
    private GuestLadderOutcome Ladder(GuestTradeHero hero, GuestTradeRequest request, int level)
    {
        if (!_shops.LadderOpen)
            return GuestLadderOutcome.Off;
        LadderSlotRules.TryParse(request.Subject, out var slot);
        var ladder = _shops.Ladder(hero);
        return request.Action == GuestTradeAction.LadderHandIn
            ? GuestLadderDesk.HandIn(ladder, hero.HeroId, slot)
            : GuestLadderDesk.Claim(ladder, hero.HeroId, hero.Player.CultureId, slot, request.Target!, level);
    }

    // The rung as the server holds it after the request. Reading it must never change the answer to the request itself.
    private GuestTradeReply WithLadder(GuestTradeReply reply, GuestTradeHero hero, int level)
    {
        if (!_shops.LadderOpen)
            return reply;
        try
        {
            return reply with
            {
                Ladder = GuestLadderDesk.View(_shops.Ladder(hero), hero.HeroId, hero.Player.CultureId, level, _shops.LadderLordLevel),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GuestTrade] the lord's gear ladder of {hero.HeroId} could not be read: {ex.Message}");
            return reply;
        }
    }

    private GuestTradeReply WithBalance(GuestTradeReply reply, string heroId, string? kingdomId, string? cultureId)
    {
        var resource = _resources.ResolveResource(kingdomId, cultureId);
        if (resource == null)
            return reply;
        return reply with
        {
            ResourceId = resource.Id,
            Balance = _resources.GetCurrentAmount(heroId, kingdomId, cultureId),
        };
    }

    private static bool IsWellFormed(GuestTradeRequest request)
    {
        if (request.Action is not (GuestTradeAction.EmissaryBalance or GuestTradeAction.EmissaryBuy
            or GuestTradeAction.ArmouryBalance or GuestTradeAction.ArmouryUpgrade
            or GuestTradeAction.LadderHandIn or GuestTradeAction.LadderClaim))
            return false;
        if (!IsId(request.RequestId) || !IsId(request.SettlementId))
            return false;
        if (!FitsOrAbsent(request.Subject) || !FitsOrAbsent(request.Modifier) || !FitsOrAbsent(request.Target))
            return false;
        return request.Action switch
        {
            GuestTradeAction.EmissaryBuy => IsId(request.Subject) && request.Quantity is >= 1 and <= MaxQuantity,
            GuestTradeAction.ArmouryUpgrade => IsId(request.Subject) && IsId(request.Target),
            GuestTradeAction.LadderHandIn => LadderSlotRules.TryParse(request.Subject, out _),
            GuestTradeAction.LadderClaim => LadderSlotRules.TryParse(request.Subject, out _) && IsId(request.Target),
            _ => true,
        };
    }

    private static bool IsId(string? text) => !string.IsNullOrEmpty(text) && text!.Length <= MaxTextLength;

    private static bool FitsOrAbsent(string? text) => text == null || text.Length <= MaxTextLength;

    private static GuestTradeReply Refuse(GuestTradeRequest request, GuestTradeRefusal refusal) =>
        new(request.RequestId ?? string.Empty, request.Action, refusal);
}
