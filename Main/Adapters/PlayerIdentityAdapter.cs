using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Adapters;

/// <inheritdoc cref="IPlayerIdentityAdapter"/>
/// <remarks>
/// Holds the feature's single reflection site: Campaign.PlayerDefaultFaction is
/// <c>internal Clan { get; set; }</c> (Campaign.cs:261) and vanilla assigns it exactly once, from
/// CampaignObjectManager. ChangePlayerCharacterAction never touches it, and Clan.PlayerClan is a
/// computed getter over it, so without this write the player clan pointer stays on the abandoned
/// character-creation clan and CharacterDeveloperVM throws enumerating its Heroes.
///
/// The probe runs in the constructor, before any UI exists, so a rename in a future engine build
/// turns the whole feature off cleanly instead of stranding a campaign mid-handover.
/// </remarks>
public class PlayerIdentityAdapter : IPlayerIdentityAdapter
{
    private readonly IModLogger _logger;
    private readonly PropertyInfo? _playerDefaultFaction;

    public PlayerIdentityAdapter(IModLogger logger)
    {
        _logger = logger;

        _playerDefaultFaction = typeof(Campaign).GetProperty(
            "PlayerDefaultFaction",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (_playerDefaultFaction?.GetSetMethod(nonPublic: true) == null)
        {
            _playerDefaultFaction = null;
            _logger.LogError(
                "Player Switcher: Campaign.PlayerDefaultFaction has no reachable setter on this engine build. The feature will stay disabled.");
        }
    }

    public bool CanReassignPlayerClan => _playerDefaultFaction != null;

    public bool IsSwitchable(string heroId)
    {
        var hero = FindHero(heroId);
        return hero != null && hero.IsAlive && hero != Hero.MainHero;
    }

    public bool StartupClanIsDisposable
    {
        get
        {
            var clan = Clan.PlayerClan;
            var player = Hero.MainHero;
            if (clan == null || player == null)
                return false;

            foreach (var member in clan.Heroes)
            {
                if (member == null || member == player)
                    continue;
                if (member.IsAlive && !member.IsChild && member.IsLord)
                    return false;
            }

            return true;
        }
    }

    public SwitchTicket Capture(string targetHeroId, string careerId)
    {
        var original = Hero.MainHero;
        if (original == null)
            return SwitchTicket.None;

        var target = FindHero(targetHeroId);

        return new SwitchTicket(
            originalHeroId: original.StringId,
            originalClanId: original.Clan?.StringId ?? string.Empty,
            originalPartyId: original.PartyBelongedTo?.StringId ?? string.Empty,
            targetClanId: target?.Clan?.StringId ?? string.Empty,
            careerId: careerId ?? string.Empty);
    }

    public void AdoptIntoPlayerClan(string heroId)
    {
        var hero = FindHero(heroId);
        var clan = Clan.PlayerClan;
        if (hero == null || clan == null)
            return;

        // Occupation first: a Wanderer who becomes a clan leader while still flagged Wanderer is
        // read as a companion by several vanilla paths.
        hero.SetNewOccupation(Occupation.Lord);

        // SetLeader assigns leader.Clan itself, so this is the whole join. The hero is clanless by
        // construction (the planner only routes clanless heroes here), so the Hero.Clan setter's
        // OnLordRemoved path on the old clan never runs and no other clan is left leaderless.
        clan.SetLeader(hero);
    }

    public void ApplyPlayerCharacter(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero != null)
            ChangePlayerCharacterAction.Apply(hero);
    }

    public void ReassignPlayerClan(string clanId)
    {
        if (_playerDefaultFaction == null || Campaign.Current == null)
            return;

        var clan = FindClan(clanId);
        if (clan == null)
        {
            _logger.LogWarning($"Player Switcher: clan '{clanId}' did not resolve; player clan pointer left alone");
            return;
        }

        _playerDefaultFaction.SetValue(Campaign.Current, clan);
    }

    public void TransferGold(string fromHeroId, string toHeroId)
    {
        var from = FindHero(fromHeroId);
        var to = FindHero(toHeroId);
        if (from == null || to == null || from.Gold <= 0)
            return;

        GiveGoldAction.ApplyBetweenCharacters(from, to, from.Gold, disableNotification: true);
    }

    public void AbsorbOriginalParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return;

        var party = MobileParty.All.FirstOrDefault(p => p.StringId == partyId);
        var main = MobileParty.MainParty;
        if (party == null || main == null || party == main)
            return;

        // Members and prisoners only. The ITEMS are deliberately not copied here.
        //
        // Vanilla SandBox ships HeirSelectionCampaignBehavior, which listens to the same
        // player-character-changed events ChangePlayerCharacterAction fires. It snapshots the old
        // main party's ItemRoster in OnBeforePlayerCharacterChanged and, when the main party
        // changed, adds it to the new one in OnPlayerCharacterChanged. So by the time we get here
        // the goods have already moved. Adding them again doubled every stack.
        main.Party.MemberRoster.Add(party.MemberRoster);
        main.Party.PrisonRoster.Add(party.PrisonRoster);

        DestroyPartyAction.Apply(null, party);
    }

    public void RemoveOriginalHero(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero == null || !hero.IsAlive)
            return;

        if (hero == Hero.MainHero)
        {
            // Would take KillCharacterAction's main-hero branches against the live player.
            _logger.LogError("Player Switcher: refusing to remove the current player character");
            return;
        }

        KillCharacterAction.ApplyByRemove(hero, showNotification: false, isForced: true);
    }

    public void MarkClanAndKingdomKnown(string heroId)
    {
        var hero = FindHero(heroId);
        var clan = hero?.Clan;
        if (clan == null)
            return;

        // Vanilla's OnPlayerCharacterChanged marks only Mother and Father, so without this the
        // clan screen opens full of unknown entries for the family the player supposedly leads.
        foreach (var member in clan.Heroes)
        {
            if (member == null)
                continue;
            member.SetHasMet();
            member.IsKnownToPlayer = true;
        }

        var kingdom = clan.Kingdom;
        if (kingdom == null)
            return;

        foreach (var peer in kingdom.Clans)
        {
            var leader = peer?.Leader;
            if (leader != null)
                leader.IsKnownToPlayer = true;
        }
    }

    /// <summary>
    /// Hides queued notifications addressed to a hero who no longer exists.
    ///
    /// NEVER call <c>InformationManager.Clear()</c> here, or anywhere else in TAOM. Despite the
    /// name it does not clear notifications: it is the process-teardown routine that NULLS every
    /// static delegate the UI subscribes to, including <c>OnShowTooltip</c>
    /// (InformationManager.cs:149 on v1.4.8). <c>ShowTooltip</c> is nothing but
    /// <c>OnShowTooltip?.Invoke(...)</c> (:74-77), so nulling it silently disables every tooltip in
    /// the game, plus every inquiry popup and every on-screen message, with no exception and no log
    /// line anywhere. It cannot recover: <c>GauntletInformationView</c> subscribes in its private
    /// constructor and <c>Initialize()</c> is guarded by <c>if (_current == null)</c>, which stays
    /// non-null, so nothing re-subscribes for the life of the process. Only restarting the game
    /// brings tooltips back.
    ///
    /// This shipped, and it cost a long investigation because the symptom is so far from the cause:
    /// tooltips were dead from the moment a taken-over campaign loaded, every input gate read
    /// healthy, every widget tree was intact, and no log said anything. Reproduced 2026-09-03 for
    /// both Faramir and Denethor; a brand new character was unaffected only because
    /// <c>HeroSwitchService.Execute</c> returns early and never reaches this method.
    /// </summary>
    public void ClearPendingNotifications()
    {
        MBInformationManager.HideInformations();
    }

    private static Hero? FindHero(string heroId)
        => string.IsNullOrEmpty(heroId)
            ? null
            : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);

    private static Clan? FindClan(string clanId)
        => string.IsNullOrEmpty(clanId)
            ? null
            : Campaign.Current?.CampaignObjectManager?.Find<Clan>(clanId);
}
