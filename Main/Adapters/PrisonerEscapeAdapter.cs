using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.CoopInterop;

namespace TAOM.Adapters;

/// <summary>Production <see cref="IPrisonerEscapeAdapter"/>: <c>EscapeTerms</c> repeats <c>PrisonerReleaseCampaignBehavior.DailyHeroTick</c>'s perk calls (v1.5.6 :223-245) and the battle-or-siege and player-clan fields are TAOM's own exclusions.</summary>
public sealed class PrisonerEscapeAdapter : IPrisonerEscapeAdapter
{
    private readonly IPlayerControlQuery _players;

    public PrisonerEscapeAdapter(IPlayerControlQuery players) => _players = players;

    public IReadOnlyList<PrisonerEscapeSnapshot> GetCapturedLords(IReadOnlyCollection<string> kingdomIds)
    {
        var result = new List<PrisonerEscapeSnapshot>();
        if (Campaign.Current == null || kingdomIds == null || kingdomIds.Count == 0)
            return result;

        var wanted = new HashSet<string>(kingdomIds);
        var playerClan = Clan.PlayerClan;
        var mainHero = Hero.MainHero;
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom == null || kingdom.IsEliminated || !wanted.Contains(kingdom.StringId))
                continue;

            var clans = kingdom.Clans;
            for (var i = 0; i < clans.Count; i++)
                ReadClan(clans[i], kingdom.StringId, playerClan, mainHero, _players, result);
        }

        return result;
    }

    public DungeonReading? ReadDungeon(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId) || Campaign.Current == null)
            return null;

        var settlement = Settlement.Find(settlementId);
        var own = settlement?.Party;
        if (settlement == null || own == null)
            return null;

        var reading = new DungeonReading { Capacity = own.PrisonerSizeLimit };
        var playerClan = Clan.PlayerClan;
        var mainHero = Hero.MainHero;

        // The parties SettlementComponent.GetPrisonerHeroes counts for vanilla's "There are N prisoners here."
        // (v1.5.6 SettlementComponent.cs:158-185): the settlement's own party and each garrison party in it.
        ReadRoster(own, reading, playerClan, mainHero, _players);
        var parties = settlement.Parties;
        for (var i = 0; i < parties.Count; i++)
        {
            if (parties[i] != null && parties[i].IsGarrison)
                ReadRoster(parties[i].Party, reading, playerClan, mainHero, _players);
        }

        return reading;
    }

    public float NextRoll() => MBRandom.RandomFloat;

    public bool Escape(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || Campaign.Current == null)
            return false;

        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroId);
        if (hero == null || !hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null
            || _players.OfHero(hero) != PlayerControl.NotPlayer)
            return false;

        EndCaptivityAction.ApplyByEscape(hero);
        return true;
    }

    private static void ReadRoster(
        PartyBase captor, DungeonReading into, Clan playerClan, Hero mainHero, IPlayerControlQuery players)
    {
        var roster = captor.PrisonRoster;
        into.HeldRegulars += roster.TotalRegulars;
        into.HeldLords += roster.TotalHeroes;
        for (var i = 0; i < roster.Count; i++)
        {
            var hero = roster.GetElementCopyAtIndex(i).Character?.HeroObject;
            if (hero == null)
                continue;

            var terms = EscapeTerms(hero, captor, true);
            into.Lords.Add(new DungeonLord
            {
                Name = hero.Name?.ToString() ?? string.Empty,
                Snapshot = Read(hero, captor, hero.Clan?.Kingdom?.StringId ?? string.Empty, playerClan, mainHero, players, terms),
                Terms = TermLines(terms),
            });
        }
    }

    private static void ReadClan(
        Clan clan, string kingdomId, Clan playerClan, Hero mainHero, IPlayerControlQuery players,
        List<PrisonerEscapeSnapshot> into)
    {
        var heroes = clan?.Heroes;
        if (heroes == null)
            return;

        for (var i = 0; i < heroes.Count; i++)
        {
            var hero = heroes[i];
            var captor = hero?.PartyBelongedToAsPrisoner;
            if (hero == null || !hero.IsPrisoner || captor == null)
                continue;
            into.Add(Read(hero, captor, kingdomId, playerClan, mainHero, players, EscapeTerms(hero, captor, false)));
        }
    }

    private static PrisonerEscapeSnapshot Read(
        Hero hero, PartyBase captor, string kingdomId, Clan playerClan, Hero mainHero, IPlayerControlQuery players,
        ExplainedNumber escapeTerms)
    {
        var mobile = captor.IsMobile;
        var settlement = mobile ? captor.MobileParty.CurrentSettlement : null;
        return new PrisonerEscapeSnapshot
        {
            HeroId = hero.StringId ?? string.Empty,
            KingdomId = kingdomId,
            IsAlive = hero.IsAlive,
            IsPrisoner = hero.IsPrisoner,
            CaptorIsMobile = mobile,
            CaptorInSettlement = settlement != null,
            CaptorHealthyMembers = mobile ? captor.NumberOfHealthyMembers : 0,
            PlayerHeld = IsPlayerHeld(captor, settlement, playerClan),
            EscapeFactor = escapeTerms.ResultNumber,
            CaptorInMapEventOrSiege = captor.MapEvent != null || captor.SiegeEvent != null,
            CanBeReleased = CanBeReleased(hero),
            IsMainHero = hero == mainHero,
            IsPlayerClan = PlayerClanCheck.IsAPlayersClan(hero.Clan, players),
        };
    }

    // The three cases at PrisonerReleaseCampaignBehavior.cs:218. Vanilla compares OwnerClan to PlayerClan
    // without a null guard, so an ownerless settlement would match a null player clan; the guard here is
    // deliberate and the same as the engine's result whenever a player clan exists.
    private static bool IsPlayerHeld(PartyBase captor, Settlement currentSettlement, Clan playerClan)
    {
        if (captor == PartyBase.MainParty)
            return true;
        if (playerClan == null)
            return false;
        if (captor.IsSettlement && captor.Settlement.OwnerClan == playerClan)
            return true;
        return captor.IsMobile && currentSettlement != null && currentSettlement.OwnerClan == playerClan;
    }

    // Vanilla's relative terms (DailyHeroTick v1.5.6 :223-245), the same calls under the same conditions, on a base of 1. Every
    // perk slot used is an AddFactor in v1.5.6's DefaultPerks, so the result is 1 + the sum of the factors. The factor is the
    // ResultNumber and the dungeon menu's lines are TermLines of the same number, built with descriptions, which change no value.
    private static ExplainedNumber EscapeTerms(Hero hero, PartyBase captor, bool includeDescriptions)
    {
        var factor = new ExplainedNumber(1f, includeDescriptions);
        if (captor.IsSettlement && captor.Settlement.Town != null
            && (captor.Settlement.IsTown || captor.Settlement.IsCastle))
        {
            var town = captor.Settlement.Town;
            PerkHelper.AddPerkBonusForTown(DefaultPerks.Roguery.SweetTalker, town, false, ref factor);
            PerkHelper.AddPerkBonusForTown(DefaultPerks.Engineering.DungeonArchitect, town, false, ref factor);
            PerkHelper.AddPerkBonusForTown(DefaultPerks.Riding.MountedPatrols, town, false, ref factor);
        }

        if (captor.IsMobile)
        {
            var party = captor.MobileParty;
            PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Roguery.FleetFooted, hero.CharacterObject, false, ref factor);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Riding.MountedPatrols, party, true, ref factor);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Roguery.RansomBroker, party, false, ref factor);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.KeenSight, party, false, ref factor);
            var leader = party.Army?.LeaderParty?.LeaderHero ?? party.LeaderHero;
            if (leader != null)
                TraitEffectHelper.ApplyTraitEffect(leader, DefaultPersonalityTraitEffects.ValorPrisonerEscapeEffect, ref factor);
        }

        return factor;
    }

    // ExplainedNumber.GetLines (v1.5.6 ExplainedNumber.cs:180, StatExplainer.GetLines :37) lists the base line first (a
    // base of 1 is not approximately zero, so the constructor adds it, :160), then each described term. AddFactor (:238)
    // stores a line as its percent and GetLines hands it back as base x percent x 0.01, which on a base of 1 is the
    // fraction itself. An empty list for a number built without descriptions. PrisonerEscapeAdapterTests pin this shape.
    internal static List<EscapeTermLine> TermLines(ExplainedNumber terms)
    {
        var lines = new List<EscapeTermLine>();
        var all = terms.GetLines();
        for (var i = 1; i < all.Count; i++)
            lines.Add(new EscapeTermLine(all[i].name, all[i].number));
        return lines;
    }

    // Fail closed: with no dispatcher the veto cannot be asked, so the lord is not released.
    private static bool CanBeReleased(Hero hero)
    {
        var dispatcher = CampaignEventDispatcher.Instance;
        if (dispatcher == null)
            return false;

        var allowed = true;
        dispatcher.CanHeroBeReleased(hero, ref allowed);
        return allowed;
    }
}
