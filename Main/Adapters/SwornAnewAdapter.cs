using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.SwornAnew.Models;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ISwornAnewAdapter"/>. Every value is a raw fact of the engine, read the way vanilla's own
/// <c>DefaultDefectionModel</c> and <c>LordDefectionCampaignBehavior.conversation_player_start_defection_with_prisoner_on_condition</c>
/// read it. The campaign statics are read through <c>Campaign.Current</c> first (adapters.md: <c>Hero.MainHero</c> and
/// <c>PartyBase.MainParty</c> throw without one). Both sides come from <c>FactionSides</c>, the id and culture of one entity: the lord by their own clan, never their realm,
/// as Kingdom Allegiance sides them (#1114), and the player by the player's realm. A read
/// that faults is logged and answers null (or false).
/// </summary>
public sealed class SwornAnewAdapter : ISwornAnewAdapter
{
    private readonly IModLogger _logger;

    public SwornAnewAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public DefectionFacts? Read(Hero? hero, Kingdom? kingdom)
    {
        try
        {
            if (hero == null || kingdom == null || Campaign.Current == null)
                return null;
            var player = CharacterObject.PlayerCharacter?.HeroObject;
            if (player == null)
                return null;

            var clan = hero.Clan;
            var faction = hero.MapFaction;
            var main = Campaign.Current.MainParty?.Party;
            var heldIn = hero.CurrentSettlement?.OwnerClan;
            return new DefectionFacts(
                HeroId: hero.StringId,
                RaceId: hero.CharacterObject?.Race,
                IsPrisoner: hero.IsPrisoner,
                InKingdom: faction != null && faction.IsKingdomFaction,
                InPlayerFaction: faction != null && faction == player.MapFaction,
                IsFactionLeader: faction != null && faction.Leader == hero,
                HasClan: clan != null,
                IsMinorFaction: clan != null && clan.IsMinorFaction,
                IsMercenary: clan != null && clan.IsUnderMercenaryService,
                ClanHasKingdom: clan?.Kingdom != null,
                TargetRuledByPlayerClan: kingdom.RulingClan != null && kingdom.RulingClan == Clan.PlayerClan,
                HasBloodFeudWithPlayer: clan != null && clan.HasBloodFeudWithPlayer,
                HeldByMainParty: main != null && hero.PartyBelongedToAsPrisoner == main,
                InPlayerClanSettlement: heldIn != null && heldIn == Clan.PlayerClan,
                OnRansomScreen: GameStateManager.Current?.LastOrDefault<PartyState>()?.PartyScreenMode == PartyScreenHelper.PartyScreenMode.Ransom,
                LordSide: FactionSides.OfOwnClan(hero),
                PlayerSide: FactionSides.OfPlayer());
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SwornAnew] Read('{hero?.StringId}') failed: {ex.Message}");
            return null;
        }
    }

    public DefectionFacts? ReadConversationPartner()
    {
        try
        {
            if (Campaign.Current == null)
                return null;
            return Read(Campaign.Current.ConversationManager?.OneToOneConversationHero, Clan.PlayerClan?.Kingdom);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SwornAnew] ReadConversationPartner failed: {ex.Message}");
            return null;
        }
    }

    public bool IsConversationPartnerJoiningPlayersKingdom(Clan? clan, Kingdom? newKingdom)
    {
        try
        {
            if (clan == null || newKingdom == null || Campaign.Current == null)
                return false;
            var partner = Campaign.Current.ConversationManager?.OneToOneConversationHero;
            return partner != null && clan.Leader == partner && newKingdom == Clan.PlayerClan?.Kingdom;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SwornAnew] IsConversationPartnerJoiningPlayersKingdom failed: {ex.Message}");
            return false;
        }
    }

    // Same shape as LordCourtesyAdapter.RunAfterConversation. The queued step cannot throw: the helper catches and logs.
    public void CommitOpenPartyScreenWhenConversationEnds()
    {
        var manager = Campaign.Current?.ConversationManager;
        if (manager == null)
            return;
        manager.ConversationEndOneShot += () => PartyScreenCommit.CommitIfMainParty(_logger, "SwornAnew");
    }
}
