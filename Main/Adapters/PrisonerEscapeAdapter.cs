using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Production <see cref="IPrisonerEscapeAdapter"/>. Every engine member read here was checked against the
/// v1.5.4 decompile: <c>PrisonerReleaseCampaignBehavior.DailyHeroTick</c> (:201-250: the gate, the three
/// player-held cases, the mobile-captor factor, and the perk and Valor factors, which
/// <c>EscapeFactor</c> repeats call for call), <c>CampaignEventDispatcher.CanHeroBeReleased</c>,
/// <c>EndCaptivityAction.ApplyByEscape</c> (:81), <c>PartyBase.MapEvent</c>, <c>PartyBase.SiegeEvent</c>,
/// <c>PartyBase.NumberOfHealthyMembers</c>, <c>Kingdom.Clans</c> and <c>Clan.Heroes</c>. The battle-or-siege
/// and player-clan fields are TAOM's own exclusions, not vanilla inputs.
/// </summary>
public sealed class PrisonerEscapeAdapter : IPrisonerEscapeAdapter
{
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
                ReadClan(clans[i], kingdom.StringId, playerClan, mainHero, result);
        }

        return result;
    }

    public float NextRoll() => MBRandom.RandomFloat;

    public bool Escape(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || Campaign.Current == null)
            return false;

        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroId);
        if (hero == null || !hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null || hero == Hero.MainHero)
            return false;

        EndCaptivityAction.ApplyByEscape(hero);
        return true;
    }

    private static void ReadClan(Clan clan, string kingdomId, Clan playerClan, Hero mainHero, List<PrisonerEscapeSnapshot> into)
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
            into.Add(Read(hero, captor, kingdomId, playerClan, mainHero));
        }
    }

    private static PrisonerEscapeSnapshot Read(Hero hero, PartyBase captor, string kingdomId, Clan playerClan, Hero mainHero)
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
            EscapeFactor = EscapeFactor(hero, captor),
            CaptorInMapEventOrSiege = captor.MapEvent != null || captor.SiegeEvent != null,
            CanBeReleased = CanBeReleased(hero),
            IsMainHero = hero == mainHero,
            IsPlayerClan = playerClan != null && hero.Clan == playerClan,
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

    // Vanilla's relative terms (DailyHeroTick v1.5.4 :223-245), the same calls under the same conditions,
    // on a base of 1. Every slot used is an AddFactor in v1.5.4 (DefaultPerks.cs:2089, 2152, 2178, 2192,
    // 2196, 2307; DefaultPersonalityTraitEffects.cs:209), so the result is 1 + the sum of the factors,
    // which multiplies the base chance exactly as vanilla's ExplainedNumber does.
    private static float EscapeFactor(Hero hero, PartyBase captor)
    {
        var factor = new ExplainedNumber(1f);
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
            PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Roguery.FleetFooted, BattleEnvironment.Any, hero.CharacterObject, false, ref factor);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Riding.MountedPatrols, party, true, ref factor);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Roguery.RansomBroker, party, false, ref factor);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.KeenSight, party, false, ref factor);
            var leader = party.Army?.LeaderParty?.LeaderHero ?? party.LeaderHero;
            if (leader != null)
                TraitEffectHelper.ApplyTraitEffect(leader, DefaultPersonalityTraitEffects.ValorPrisonerEscapeEffect, ref factor);
        }

        return factor.ResultNumber;
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
