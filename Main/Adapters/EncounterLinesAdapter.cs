using System;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines.Models;

namespace TAOM.Adapters;

/// <summary>
/// Encounter Lines' window on the campaign (Patch149). Every read starts from <c>Campaign.Current</c> and reads the main
/// hero through <c>CharacterObject.PlayerCharacter</c>, so no call throws outside a campaign. Vanilla's private
/// <c>_interactedCaravans</c> and <c>_interactedVillagers</c> are bound once as <c>object</c> read as an
/// <see cref="System.Collections.IDictionary"/> (their value type is a private enum, as in <see cref="BanditEncounterTextAdapter"/>);
/// a failed binding logs once and reads as not a first meeting. A singleton that keeps no engine object between calls.
/// </summary>
public sealed class EncounterLinesAdapter : IEncounterLinesAdapter
{
    private readonly IRaceManager _races;
    private readonly AccessTools.FieldRef<CaravansCampaignBehavior, object>? _interactedCaravans;
    private readonly AccessTools.FieldRef<VillagerCampaignBehavior, object>? _interactedVillagers;

    public EncounterLinesAdapter(IRaceManager races, IModLogger logger)
    {
        _races = races;
        _interactedCaravans = Bind<CaravansCampaignBehavior>("_interactedCaravans", logger);
        _interactedVillagers = Bind<VillagerCampaignBehavior>("_interactedVillagers", logger);
    }

    /// <summary>
    /// The lord a conversation tag is asked about, or null for a character that is not a hero or with no campaign. Not on
    /// <see cref="IEncounterLinesAdapter"/>, because the tag hands over an engine character (ADR-007); the tag gate calls it
    /// through a delegate.
    /// </summary>
    public LordFacts? DescribeLord(CharacterObject? character)
    {
        if (Campaign.Current == null || character == null || !character.IsHero)
            return null;
        var hero = character.HeroObject;
        var main = CharacterObject.PlayerCharacter?.HeroObject;
        if (hero == null || main == null)
            return null;
        // Only lords speak lines; a notable or wanderer stops here, before UnderPlayerCommand, which reads the hero's home.
        if (!hero.IsLord)
            return null;

        var faction = hero.MapFaction;
        var playerFaction = main.MapFaction;
        return new LordFacts(
            hero.IsLord,
            hero.Clan != null && hero.Clan == main.Clan,
            HeroHelper.UnderPlayerCommand(hero),
            hero.Culture?.StringId ?? string.Empty,
            RaceName(character.Race),
            FactionSides.OfHero(hero),
            faction != null && playerFaction != null && FactionManager.IsAtWarAgainstFaction(faction, playerFaction),
            faction != null && faction.IsKingdomFaction && faction == playerFaction && playerFaction.Leader == main,
            hero.StringId ?? string.Empty);
    }

    public PlayerFacts? Player()
    {
        if (Campaign.Current == null)
            return null;
        var main = CharacterObject.PlayerCharacter?.HeroObject;
        if (main == null)
            return null;

        return new PlayerFacts(main.Culture?.StringId ?? string.Empty, RaceName(main.CharacterObject?.Race ?? -1), FactionSides.OfPlayer());
    }

    public PartyFacts? ConversationParty()
    {
        if (Campaign.Current == null)
            return null;
        var party = Campaign.Current.ConversationManager?.ConversationParty;
        var main = CharacterObject.PlayerCharacter?.HeroObject;
        if (party == null || main == null)
            return null;

        var faction = party.MapFaction;
        var playerFaction = main.MapFaction;
        return new PartyFacts(
            party.HomeSettlement?.Culture?.StringId ?? string.Empty,
            faction != null && playerFaction != null && FactionManager.IsAtWarAgainstFaction(faction, playerFaction),
            party.IsCurrentlyAtSea,
            party.Owner == main || (party.ActualClan != null && party.ActualClan == main.Clan));
    }

    public bool IsFirstCaravanMeeting()
    {
        var accessor = _interactedCaravans;
        var behavior = Campaign.Current?.GetCampaignBehavior<CaravansCampaignBehavior>();
        if (accessor == null || behavior == null)
            return false;
        return BanditEncounterTextAdapter.FirstMeeting(accessor(behavior), Campaign.Current!.ConversationManager?.ConversationParty);
    }

    public bool IsFirstVillagerMeeting()
    {
        var accessor = _interactedVillagers;
        var behavior = Campaign.Current?.GetCampaignBehavior<VillagerCampaignBehavior>();
        if (accessor == null || behavior == null)
            return false;
        return BanditEncounterTextAdapter.FirstMeeting(accessor(behavior), PlayerEncounter.EncounteredParty?.MobileParty);
    }

    public string? EncounteredFactionId => Campaign.Current == null ? null : PlayerEncounter.EncounteredParty?.MapFaction?.StringId;

    public string? HideoutBossClanId
    {
        get
        {
            var conversation = Campaign.Current?.ConversationManager;
            if (conversation == null)
                return null;
            // A mission conversation (the hideout boss) has no ConversationParty; the boss agent's origin holds his party.
            var agent = conversation.OneToOneConversationAgent as Agent;
            var clan = (agent?.Character as CharacterObject)?.HeroObject?.Clan
                ?? conversation.ConversationParty?.ActualClan
                ?? (agent?.Origin?.BattleCombatant as PartyBase)?.MobileParty?.ActualClan;
            return clan?.StringId;
        }
    }

    public bool EncounteredAtSea => Campaign.Current != null && (PlayerEncounter.EncounteredMobileParty?.IsCurrentlyAtSea ?? false);

    public bool InSettlement => Campaign.Current != null && Settlement.CurrentSettlement != null;

    public int DayOfYear => Campaign.Current == null ? 0 : CampaignTime.Now.GetDayOfYear;

    public bool HasText(string textId, string variation) => GameTexts.TryGetText(textId, out _, variation);

    public bool TrySetVariable(string variable, string textId, string variation)
    {
        if (!GameTexts.TryGetText(textId, out var text, variation))
            return false;

        MBTextManager.SetTextVariable(variable, text);
        return true;
    }

    /// <summary>The race name for a race id the table holds; empty otherwise, because <c>GetRaceNameFromId</c> turns an unknown id into "human".</summary>
    private string RaceName(int raceId) => _races.IsValidRaceId(raceId) ? _races.GetRaceNameFromId(raceId) : string.Empty;

    /// <summary>Binds one of vanilla's private meeting records; null (and one warning) when the field is gone.</summary>
    internal static AccessTools.FieldRef<T, object>? Bind<T>(string field, IModLogger logger)
    {
        try
        {
            return AccessTools.FieldRefAccess<T, object>(field);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[EncounterLines] " + typeof(T).Name + "." + field + " did not bind, its greetings stay vanilla: " + ex.Message);
            return null;
        }
    }
}
