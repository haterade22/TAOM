using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.LordRetinue.Hooks;

/// <summary>
/// Engine reads and writes for LordRetinue (ADR-002): reads an AI lord party and the heroes of its clan into
/// engine-free snapshots, and applies the service's decisions with the vanilla actions. The behavior has
/// already decided that the party is an AI lord party this peer may change. Only the entry point uses it, so
/// it has no interface: no test fakes it and nothing else implements it. It keeps no engine object in a
/// field. Main thread only.
/// </summary>
public sealed class LordRetinueWorld
{
    private readonly IPlayerControlQuery _playerControl;

    public LordRetinueWorld(IPlayerControlQuery playerControl)
    {
        _playerControl = playerControl;
    }

    /// <summary>Null when the party is not at rest in a settlement: no leader (a disbanding party has none), in a map event, besieging, flagged disbanding, or the settlement is besieged.</summary>
    public RetinueParty? ReadParty(MobileParty party)
    {
        var leader = party.LeaderHero;
        var clan = party.ActualClan;
        var settlement = party.CurrentSettlement;
        if (leader == null || clan == null || settlement == null)
            return null;
        if (party.MapEvent != null || party.BesiegerCamp != null || party.IsDisbanding || settlement.IsUnderSiege)
            return null;

        var filled = RetinueRole.None;
        List<string>? retinue = null;
        ReadRole(party, leader, PartyRole.Scout, RetinueRole.Scout, ref filled, ref retinue);
        ReadRole(party, leader, PartyRole.Surgeon, RetinueRole.Surgeon, ref filled, ref retinue);
        ReadRole(party, leader, PartyRole.Engineer, RetinueRole.Engineer, ref filled, ref retinue);
        ReadRole(party, leader, PartyRole.Quartermaster, RetinueRole.Quartermaster, ref filled, ref retinue);

        var extra = party.MemberRoster.TotalHeroes - 1;
        var limit = Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(clan, clan.Tier);
        return new RetinueParty(
            extra < 0 ? 0 : extra,
            clan.WarPartyComponents.Count < limit,
            filled,
            ReadSkills(leader),
            retinue ?? (IReadOnlyList<string>)Array.Empty<string>());
    }

    /// <summary>Facts about every hero of the party's clan, for <see cref="LordRetinueService.Choose"/>.</summary>
    public IReadOnlyList<RetinueHero> ReadCandidates(MobileParty party)
    {
        var clan = party.ActualClan;
        var settlement = party.CurrentSettlement;
        if (clan == null || settlement == null)
            return Array.Empty<RetinueHero>();

        var comesOfAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
        var heroes = new List<RetinueHero>();
        foreach (var hero in clan.Heroes)
        {
            heroes.Add(new RetinueHero(hero.StringId, ReadSkills(hero))
            {
                InPartySettlement = hero.CurrentSettlement == settlement,
                IsActive = hero.IsActive,
                IsAlive = hero.IsAlive,
                IsAdult = hero.Age >= comesOfAge,
                CanMoveToSettlement = hero.CanMoveToSettlement(),
                CanHavePartyRole = hero.CanBeGovernorOrHavePartyRole(),
                HasParty = hero.PartyBelongedTo != null,
                IsPrisoner = hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null,
                IsPlayerControlled = _playerControl.OfHero(hero) != PlayerControl.NotPlayer,
                IsTemplate = hero.IsTemplate,
                IsNotable = hero.IsNotable,
                IsWanderer = hero.IsWanderer,
                IsWounded = hero.IsWounded,
                IsNoncombatant = hero.IsNoncombatant,
                IsGovernor = hero.GovernorOf != null,
                IsClanLeader = hero == clan.Leader,
            });
        }

        return heroes;
    }

    /// <summary>Puts the hero in the party roster and gives the hero the role.</summary>
    public void Join(MobileParty party, string heroId, RetinueRole role)
    {
        var hero = FindClanHero(party, heroId);
        if (hero == null)
            return;

        AddHeroToPartyAction.Apply(hero, party, showNotification: false);
        party.SetHeroPartyRole(hero, ToPartyRole(role));
    }

    /// <summary>Moves the listed heroes out of the party into its settlement.</summary>
    public void Release(MobileParty party, IReadOnlyList<string> heroIds)
    {
        var settlement = party.CurrentSettlement;
        if (settlement == null)
            return;

        foreach (var heroId in heroIds)
        {
            var hero = FindClanHero(party, heroId);
            if (hero != null && hero.PartyBelongedTo == party)
                TeleportHeroAction.ApplyImmediateTeleportToSettlement(hero, settlement);
        }
    }

    /// <summary>
    /// Takes every hero except the leader out of a party whose disband just started: into the settlement the
    /// party sits in, else as a fugitive, as vanilla's own disband check does away from a settlement. The
    /// garrison merge copies the whole roster and then clears it, which leaves a hero listed in the garrison
    /// with no party. This runs at the start, not at the merge, so it does not depend on listener order.
    /// </summary>
    public void ReleaseAtDisband(MobileParty party)
    {
        var leader = party.LeaderHero;
        var settlement = party.CurrentSettlement;
        var heroes = new List<Hero>();
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            var hero = element.Character?.HeroObject;
            if (hero != null && hero != leader && hero.IsAlive && _playerControl.OfHero(hero) == PlayerControl.NotPlayer)
                heroes.Add(hero);
        }

        foreach (var hero in heroes)
        {
            // A party at rest in a settlement puts the hero there at once. Away from one, or when the teleport
            // declines (the action returns early for a party in a map event), vanilla's own release applies:
            // the hero turns fugitive and returns to a settlement within a few days.
            if (settlement != null)
                TeleportHeroAction.ApplyImmediateTeleportToSettlement(hero, settlement);
            if (hero.PartyBelongedTo == party)
                MakeHeroFugitiveAction.Apply(hero);
        }
    }

    private static void ReadRole(
        MobileParty party, Hero leader, PartyRole partyRole, RetinueRole role,
        ref RetinueRole filled, ref List<string>? retinue)
    {
        var holder = party.GetRoleHolder(partyRole);
        if (holder == null || holder.PartyBelongedTo != party)
            return;

        filled |= role;
        if (holder == leader || holder.Clan != party.ActualClan)
            return;
        retinue ??= new List<string>();
        if (!retinue.Contains(holder.StringId))
            retinue.Add(holder.StringId);
    }

    private static RetinueSkills ReadSkills(Hero hero) => new(
        hero.GetSkillValue(DefaultSkills.Scouting), hero.GetSkillValue(DefaultSkills.Medicine),
        hero.GetSkillValue(DefaultSkills.Engineering), hero.GetSkillValue(DefaultSkills.Steward));

    private static Hero? FindClanHero(MobileParty party, string heroId)
    {
        var clan = party.ActualClan;
        if (clan == null)
            return null;
        foreach (var hero in clan.Heroes)
        {
            if (hero.StringId == heroId)
                return hero;
        }

        return null;
    }

    private static PartyRole ToPartyRole(RetinueRole role) => role switch
    {
        RetinueRole.Scout => PartyRole.Scout,
        RetinueRole.Surgeon => PartyRole.Surgeon,
        RetinueRole.Engineer => PartyRole.Engineer,
        _ => PartyRole.Quartermaster,
    };
}
