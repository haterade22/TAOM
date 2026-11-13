using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

/// <summary>
/// The one place that reads a target hero from the engine for the messenger system (#898): the validation snapshot and
/// the price inputs, for the dialog, the send and the encyclopedia button alike. Engine reads stay in this boundary;
/// the service decides from plain values.
/// </summary>
internal static class MessengerTargetReader
{
    /// <summary>Facts only; whether "together" refuses a send is the service's rule (<c>inPerson</c>).</summary>
    internal static HeroSnapshot? ReadSnapshot(Hero? hero)
    {
        if (hero == null) return null;
        var mainParty = Campaign.Current?.MainParty;
        var inPlayerParty = hero.PartyBelongedTo != null
                            && Hero.MainHero?.PartyBelongedTo != null
                            && hero.PartyBelongedTo == Hero.MainHero.PartyBelongedTo;
        var here = mainParty?.CurrentSettlement;
        var army = mainParty?.Army;
        return new HeroSnapshot(
            heroId: hero.StringId,
            isAlive: hero.IsAlive,
            isPrisoner: hero.IsPrisoner,
            isChild: hero.IsChild,
            isFugitive: hero.IsFugitive,
            isActive: hero.IsActive,
            isWanderer: hero.IsWanderer,
            isHumanPlayerCharacter: hero.IsHumanPlayerCharacter,
            isInPlayerParty: inPlayerParty,
            sameSettlement: here != null && hero.CurrentSettlement == here,
            sameArmy: army != null && mainParty != null && hero.PartyBelongedTo != null
                      && army.DoesLeaderPartyAndAttachedPartiesContain(mainParty)
                      && army.DoesLeaderPartyAndAttachedPartiesContain(hero.PartyBelongedTo),
            heroState: (MessengerHeroState)(int)hero.HeroState);
    }

    internal static MessengerQuoteInput ReadQuoteInput(Hero? hero, int travelDays)
    {
        var distance = float.NaN;
        var from = Hero.MainHero?.GetMapPoint();
        var to = hero?.GetMapPoint();
        if (from != null && to != null)
            distance = from.Position.Distance(to.Position);

        // IsAtWarAgainstFaction answers false for a null faction.
        var atWar = FactionManager.IsAtWarAgainstFaction(hero?.MapFaction, Hero.MainHero?.MapFaction);

        return new MessengerQuoteInput(
            Distance: distance,
            MapDiagonal: Campaign.MapDiagonal,
            TravelDays: travelDays,
            AtWar: atWar,
            NeverMet: hero != null && !hero.HasMet,
            Besieged: hero?.CurrentSettlement?.IsUnderSiege == true);
    }
}
